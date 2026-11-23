
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Net;
using Microsoft.Extensions.Options;
using SonicMemoryMaps.Application.Abstractions;

namespace SonicMemoryMaps.Infrastructure.Storage;

public sealed class SupabaseAudioStorageService : IAudioStorageService
{
    private static readonly TimeSpan UploadUrlLifetime =
        TimeSpan.FromHours(2);

    private static readonly TimeSpan DownloadUrlLifetime =
        TimeSpan.FromMinutes(5);

    private static readonly IReadOnlyDictionary<string, string> AudioExtensions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["audio/mp4"] = ".m4a",
            ["audio/mpeg"] = ".mp3",
            ["audio/aac"] = ".aac",
            ["audio/wav"] = ".wav",
            ["audio/ogg"] = ".ogg",
            ["audio/webm"] = ".webm"
        };

    private readonly HttpClient _httpClient;
    private readonly SupabaseOptions _supabaseOptions;
    private readonly SupabaseStorageOptions _storageOptions;

    public SupabaseAudioStorageService(
        HttpClient httpClient,
        IOptions<SupabaseOptions> supabaseOptions,
        IOptions<SupabaseStorageOptions> storageOptions)
    {
        _httpClient = httpClient;
        _supabaseOptions = supabaseOptions.Value;
        _storageOptions = storageOptions.Value;
    }

    public async Task<AudioUploadAuthorization> CreateUploadAuthorizationAsync(
        Guid userId,
        Guid memoryId,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID is required.", nameof(userId));

        if (memoryId == Guid.Empty)
            throw new ArgumentException("Memory ID is required.", nameof(memoryId));

        if (!AudioExtensions.TryGetValue(contentType, out var extension))
        {
            throw new ArgumentException(
                "Unsupported audio content type.",
                nameof(contentType));
        }

        var objectPath =
            $"{userId:D}/{memoryId:D}{extension}";

        var encodedBucket =
            Uri.EscapeDataString(_storageOptions.Bucket);

        var encodedPath = string.Join(
            "/",
            objectPath.Split('/').Select(Uri.EscapeDataString));

        var storageBaseUrl =
            $"{_supabaseOptions.Url.TrimEnd('/')}/storage/v1";

        var requestUrl =
            $"{storageBaseUrl}/object/upload/sign/{encodedBucket}/{encodedPath}";

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            requestUrl);

        request.Headers.Add("apikey", _supabaseOptions.SecretKey);

        request.Content = new StringContent(
            "{}",
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Supabase Storage failed to create an upload URL. " +
                $"HTTP status: {(int)response.StatusCode}.");
        }

        await using var responseStream =
            await response.Content.ReadAsStreamAsync(cancellationToken);

        using var json = await JsonDocument.ParseAsync(
            responseStream,
            cancellationToken: cancellationToken);

        if (!json.RootElement.TryGetProperty("url", out var urlElement))
        {
            throw new InvalidOperationException(
                "Supabase Storage response did not contain an upload URL.");
        }

        var returnedUrl = urlElement.GetString();

        if (string.IsNullOrWhiteSpace(returnedUrl))
        {
            throw new InvalidOperationException(
                "Supabase Storage returned an empty upload URL.");
        }

        var storageBaseUri = new Uri(storageBaseUrl + "/");

        var uploadUrl = new Uri(
            storageBaseUri,
            returnedUrl.TrimStart('/'));

        var expectedUploadPath =
            $"{storageBaseUri.AbsolutePath.TrimEnd('/')}" +
            $"/object/upload/sign/{encodedBucket}/{encodedPath}";

        if (uploadUrl.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(
                uploadUrl.Host,
                storageBaseUri.Host,
                StringComparison.OrdinalIgnoreCase) ||
            uploadUrl.Port != storageBaseUri.Port ||
            !string.IsNullOrEmpty(uploadUrl.UserInfo) ||
            !string.IsNullOrEmpty(uploadUrl.Fragment) ||
            !string.Equals(
                uploadUrl.AbsolutePath,
                expectedUploadPath,
                StringComparison.Ordinal) ||
            !HasValidSignedUrlToken(uploadUrl))
        {
            throw new InvalidOperationException(
                "Supabase Storage returned an unexpected upload URL.");
        }

        return new AudioUploadAuthorization(
            objectPath,
            uploadUrl.ToString(),
            DateTimeOffset.UtcNow.Add(UploadUrlLifetime));
    }

    public async Task<AudioDownloadAuthorization> CreateDownloadAuthorizationAsync(
        Guid userId,
        Guid memoryId,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID is required.", nameof(userId));

        if (memoryId == Guid.Empty)
            throw new ArgumentException("Memory ID is required.", nameof(memoryId));

        if (!AudioExtensions.TryGetValue(contentType, out var extension))
        {
            throw new ArgumentException(
                "Unsupported audio content type.",
                nameof(contentType));
        }

        var objectPath = $"{userId:D}/{memoryId:D}{extension}";

        var encodedBucket = Uri.EscapeDataString(_storageOptions.Bucket);

        var encodedPath = string.Join(
            "/",
            objectPath.Split('/').Select(Uri.EscapeDataString));

        var storageBaseUrl =
            $"{_supabaseOptions.Url.TrimEnd('/')}/storage/v1";

        var requestUrl =
            $"{storageBaseUrl}/object/sign/{encodedBucket}/{encodedPath}";

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            requestUrl);

        request.Headers.Add("apikey", _supabaseOptions.SecretKey);

        request.Content = new StringContent(
            """{"expiresIn":300}""",
            Encoding.UTF8,
            "application/json");

        var expiresAt = DateTimeOffset.UtcNow.Add(DownloadUrlLifetime);

        using var response = await _httpClient.SendAsync(
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Supabase Storage failed to create a download URL. " +
                $"HTTP status: {(int)response.StatusCode}.");
        }

        await using var responseStream =
            await response.Content.ReadAsStreamAsync(cancellationToken);

        using var json = await JsonDocument.ParseAsync(
            responseStream,
            cancellationToken: cancellationToken);

        if (!json.RootElement.TryGetProperty(
            "signedURL",
            out var urlElement))
        {
            throw new InvalidOperationException(
                "Supabase Storage response did not contain a signed download URL.");
        }

        var returnedUrl = urlElement.GetString();

        if (string.IsNullOrWhiteSpace(returnedUrl))
        {
            throw new InvalidOperationException(
                "Supabase Storage returned an empty download URL.");
        }

        var storageBaseUri = new Uri(storageBaseUrl + "/");

        var downloadUrl = new Uri(
            storageBaseUri,
            returnedUrl.TrimStart('/'));

        var expectedDownloadPath =
            $"{storageBaseUri.AbsolutePath.TrimEnd('/')}" +
            $"/object/sign/{encodedBucket}/{encodedPath}";

        if (downloadUrl.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(
                downloadUrl.Host,
                storageBaseUri.Host,
                StringComparison.OrdinalIgnoreCase) ||
            downloadUrl.Port != storageBaseUri.Port ||
            !string.IsNullOrEmpty(downloadUrl.UserInfo) ||
            !string.IsNullOrEmpty(downloadUrl.Fragment) ||
            !string.Equals(
                downloadUrl.AbsolutePath,
                expectedDownloadPath,
                StringComparison.Ordinal) ||
            !HasValidSignedUrlToken(downloadUrl))
        {
            throw new InvalidOperationException(
                "Supabase Storage returned an unexpected download URL.");
        }

        return new AudioDownloadAuthorization(
            downloadUrl.ToString(),
            expiresAt);
    }

    public async Task<AudioObjectMetadata?> GetObjectMetadataAsync(
        Guid userId,
        Guid memoryId,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID is required.", nameof(userId));

        if (memoryId == Guid.Empty)
            throw new ArgumentException("Memory ID is required.", nameof(memoryId));

        if (!AudioExtensions.TryGetValue(contentType, out var extension))
        {
            throw new ArgumentException(
                "Unsupported audio content type.",
                nameof(contentType));
        }

        var objectPath = $"{userId:D}/{memoryId:D}{extension}";

        var encodedBucket = Uri.EscapeDataString(_storageOptions.Bucket);

        var encodedPath = string.Join(
            "/",
            objectPath.Split('/').Select(Uri.EscapeDataString));

        var storageBaseUrl =
            $"{_supabaseOptions.Url.TrimEnd('/')}/storage/v1";

        var requestUrl =
            $"{storageBaseUrl}/object/info/{encodedBucket}/{encodedPath}";

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            requestUrl);

        request.Headers.Add("apikey", _supabaseOptions.SecretKey);

        using var response = await _httpClient.SendAsync(
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Supabase Storage failed to retrieve object metadata. " +
                $"HTTP status: {(int)response.StatusCode}.");
        }

        await using var responseStream =
            await response.Content.ReadAsStreamAsync(cancellationToken);

        using var json = await JsonDocument.ParseAsync(
            responseStream,
            cancellationToken: cancellationToken);

        var root = json.RootElement;

        if (!root.TryGetProperty("name", out var nameElement) ||
            !root.TryGetProperty("metadata", out var metadataElement))
        {
            throw new InvalidOperationException(
                "Supabase Storage returned incomplete object metadata.");
        }

        var objectName = nameElement.GetString();

        if (!string.Equals(
            objectName,
            objectPath,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Supabase Storage returned an unexpected object path.");
        }

        if (!metadataElement.TryGetProperty("size", out var sizeElement) ||
            !sizeElement.TryGetInt64(out var sizeBytes))
        {
            throw new InvalidOperationException(
                "Supabase Storage returned an invalid object size.");
        }

        const long MaxAudioSizeBytes = 52_428_800;

        if (sizeBytes < 1 || sizeBytes > MaxAudioSizeBytes)
        {
            throw new InvalidOperationException(
                "Supabase Storage returned an object with an invalid size.");
        }

        if (!metadataElement.TryGetProperty("mimetype", out var mimeElement))
        {
            throw new InvalidOperationException(
                "Supabase Storage did not return the object MIME type.");
        }

        var actualContentType = mimeElement.GetString();

        if (string.IsNullOrWhiteSpace(actualContentType))
        {
            throw new InvalidOperationException(
                "Supabase Storage returned an empty MIME type.");
        }

        if (!string.Equals(
            actualContentType,
            contentType,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Supabase Storage returned an unexpected MIME type.");
        }

        return new AudioObjectMetadata(
            objectPath,
            actualContentType,
            sizeBytes);
    }

    private static bool HasValidSignedUrlToken(Uri signedUrl)
    {
        var query = signedUrl.Query.TrimStart('?');

        return query
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Any(parameter =>
            {
                var parts = parameter.Split('=', 2);

                return parts.Length == 2 &&
                       Uri.UnescapeDataString(parts[0]) == "token" &&
                       !string.IsNullOrWhiteSpace(
                           Uri.UnescapeDataString(parts[1]));
            });
    }
}
