using SonicMemoryMaps.Application.Abstractions;

namespace SonicMemoryMaps.IntegrationTests.Storage;

public sealed class FakeAudioStorageService : IAudioStorageService
{
    public int CreateAuthorizationCallCount { get; private set; }

    public Guid? LastUserId { get; private set; }

    public Guid? LastMemoryId { get; private set; }

    public string? LastContentType { get; private set; }

    public AudioObjectMetadata? Metadata { get; set; }

    public int GetMetadataCallCount { get; private set; }

    public Task<AudioUploadAuthorization> CreateUploadAuthorizationAsync(
        Guid userId,
        Guid memoryId,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        CreateAuthorizationCallCount++;

        LastUserId = userId;
        LastMemoryId = memoryId;
        LastContentType = contentType;

        var extension = contentType.ToLowerInvariant() switch
        {
            "audio/mp4" => ".m4a",
            "audio/mpeg" => ".mp3",
            "audio/aac" => ".aac",
            "audio/wav" => ".wav",
            "audio/ogg" => ".ogg",
            "audio/webm" => ".webm",
            _ => throw new ArgumentException("Unsupported content type.")
        };

        return Task.FromResult(
            new AudioUploadAuthorization(
                $"{userId:D}/{memoryId:D}{extension}",
                "https://test-project.supabase.co/storage/v1/upload/signed/test-token",
                DateTimeOffset.UtcNow.AddHours(2)));
    }

    public Task<AudioObjectMetadata?> GetObjectMetadataAsync(
    Guid userId,
    Guid memoryId,
    string contentType,
    CancellationToken cancellationToken = default)
    {
        GetMetadataCallCount++;

        return Task.FromResult(Metadata);
    }
}