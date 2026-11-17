
using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using SonicMemoryMaps.Infrastructure.Storage;
using SonicMemoryMaps.Application.Abstractions;

namespace SonicMemoryMaps.UnitTests.Storage;

public sealed class SupabaseAudioStorageServiceTests
{
    private const string SupabaseUrl = "https://test-project.supabase.co";
    private const string SecretKey = "test-secret-key";
    private const string Bucket = "memory-audio";

    private static readonly Guid UserId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly Guid MemoryId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static SupabaseAudioStorageService CreateService(
        HttpMessageHandler handler)
    {
        var client = new HttpClient(handler);

        return new SupabaseAudioStorageService(
            client,
            Options.Create(new SupabaseOptions
            {
                Url = SupabaseUrl,
                SecretKey = SecretKey
            }),
            Options.Create(new SupabaseStorageOptions
            {
                Bucket = Bucket
            }));
    }

    [Fact]
    public async Task CreateUploadAuthorizationAsync_ReturnsUploadAuthorization()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);

            Assert.Equal(
                $"{SupabaseUrl}/storage/v1/object/upload/sign/{Bucket}/{UserId:D}/{MemoryId:D}.mp3",
                request.RequestUri!.ToString());

            Assert.Equal(
                SecretKey,
                request.Headers.GetValues("apikey").Single());

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""{"url":"/object/upload/sign/{{Bucket}}/{{UserId:D}}/{{MemoryId:D}}.mp3?token=test-token"}""")
            });
        });

        var service = CreateService(handler);

        // Act
        var result = await service.CreateUploadAuthorizationAsync(
            UserId,
            MemoryId,
            "audio/mpeg");

        // Assert
        Assert.Equal(
            $"{UserId:D}/{MemoryId:D}.mp3",
            result.ObjectPath);

        var uploadUri = new Uri(result.UploadUrl);

        Assert.Equal("https", uploadUri.Scheme);
        Assert.Equal("test-project.supabase.co", uploadUri.Host);
        Assert.Contains("token=test-token", result.UploadUrl);

        Assert.InRange(
            result.ExpiresAt,
            DateTimeOffset.UtcNow.AddHours(1).AddMinutes(59),
            DateTimeOffset.UtcNow.AddHours(2).AddSeconds(5));
    }

    [Theory]
    [InlineData("audio/mp4", ".m4a")]
    [InlineData("audio/mpeg", ".mp3")]
    [InlineData("audio/aac", ".aac")]
    [InlineData("audio/wav", ".wav")]
    [InlineData("audio/ogg", ".ogg")]
    [InlineData("audio/webm", ".webm")]
    public async Task CreateUploadAuthorizationAsync_UsesCorrectExtension(
        string contentType,
        string expectedExtension)
    {
        // Arrange
        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""{"url":"/object/upload/sign/{{Bucket}}/{{UserId:D}}/{{MemoryId:D}}{{expectedExtension}}?token=abc"}""")
            }));

        var service = CreateService(handler);

        // Act
        var result = await service.CreateUploadAuthorizationAsync(
            UserId,
            MemoryId,
            contentType);

        // Assert
        Assert.EndsWith(expectedExtension, result.ObjectPath);
    }

    [Fact]
    public async Task CreateUploadAuthorizationAsync_RejectsEmptyUserId()
    {
        var service = CreateService(new FakeHttpMessageHandler(
            (_, _) => throw new InvalidOperationException(
                "HTTP request should not be sent.")));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateUploadAuthorizationAsync(
                Guid.Empty,
                MemoryId,
                "audio/mpeg"));
    }

    [Fact]
    public async Task CreateUploadAuthorizationAsync_RejectsEmptyMemoryId()
    {
        var service = CreateService(new FakeHttpMessageHandler(
            (_, _) => throw new InvalidOperationException(
                "HTTP request should not be sent.")));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateUploadAuthorizationAsync(
                UserId,
                Guid.Empty,
                "audio/mpeg"));
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("audio/flac")]
    [InlineData("")]
    public async Task CreateUploadAuthorizationAsync_RejectsUnsupportedContentType(
        string contentType)
    {
        var service = CreateService(new FakeHttpMessageHandler(
            (_, _) => throw new InvalidOperationException(
                "HTTP request should not be sent.")));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateUploadAuthorizationAsync(
                UserId,
                MemoryId,
                contentType));
    }

    [Fact]
    public async Task CreateUploadAuthorizationAsync_ThrowsWhenSupabaseReturnsError()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(
                HttpStatusCode.InternalServerError)));

        var service = CreateService(handler);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.CreateUploadAuthorizationAsync(
                UserId,
                MemoryId,
                "audio/mpeg"));

        Assert.Contains("500", exception.Message);
    }

    [Fact]
    public async Task CreateUploadAuthorizationAsync_ThrowsWhenResponseHasNoUrl()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"token":"test-token"}""")
            }));

        var service = CreateService(handler);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateUploadAuthorizationAsync(
                UserId,
                MemoryId,
                "audio/mpeg"));
    }

    [Fact]
    public async Task CreateUploadAuthorizationAsync_RejectsUnexpectedUploadHost()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"url":"https://malicious.example/upload?token=test"}""")
            }));

        var service = CreateService(handler);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateUploadAuthorizationAsync(
                UserId,
                MemoryId,
                "audio/mpeg"));
    }


    [Fact]
    public async Task CreateUploadAuthorizationAsync_RejectsHttpUploadUrl()
    {
        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"url":"http://test-project.supabase.co/object/upload/sign/test?token=abc"}""")
            }));

        var service = CreateService(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateUploadAuthorizationAsync(
                UserId,
                MemoryId,
                "audio/mpeg"));
    }

    [Fact]
    public async Task CreateUploadAuthorizationAsync_RejectsUnexpectedPath()
    {
        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"url":"https://test-project.supabase.co/unexpected/path?token=abc"}""")
            }));

        var service = CreateService(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateUploadAuthorizationAsync(
                UserId,
                MemoryId,
                "audio/mpeg"));
    }

    [Fact]
    public async Task CreateUploadAuthorizationAsync_RejectsEmptyUrl()
    {
        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"url":" "}""")
            }));

        var service = CreateService(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateUploadAuthorizationAsync(
                UserId,
                MemoryId,
                "audio/mpeg"));
    }

    [Fact]
    public async Task GetObjectMetadataAsync_ReturnsObjectMetadata()
    {
        // Arrange
        var expectedPath = $"{UserId:D}/{MemoryId:D}.mp3";

        var handler = new FakeHttpMessageHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);

            Assert.Equal(
                $"{SupabaseUrl}/storage/v1/object/info/{Bucket}/{expectedPath}",
                request.RequestUri!.ToString());

            Assert.Equal(
                SecretKey,
                request.Headers.GetValues("apikey").Single());

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""
                {
                    "name": "{{expectedPath}}",
                    "metadata": {
                        "size": 1024000,
                        "mimetype": "audio/mpeg"
                    }
                }
                """)
            });
        });

        var service = CreateService(handler);

        // Act
        var result = await service.GetObjectMetadataAsync(
            UserId,
            MemoryId,
            "audio/mpeg");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedPath, result.ObjectPath);
        Assert.Equal("audio/mpeg", result.ContentType);
        Assert.Equal(1024000, result.SizeBytes);
    }

    [Fact]
    public async Task GetObjectMetadataAsync_ReturnsNullWhenObjectDoesNotExist()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

        var service = CreateService(handler);

        // Act
        var result = await service.GetObjectMetadataAsync(
            UserId,
            MemoryId,
            "audio/mpeg");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetObjectMetadataAsync_RejectsEmptyUserId()
    {
        var service = CreateService(new FakeHttpMessageHandler(
            (_, _) => throw new InvalidOperationException(
                "HTTP request should not be sent.")));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetObjectMetadataAsync(
                Guid.Empty,
                MemoryId,
                "audio/mpeg"));
    }

    [Fact]
    public async Task GetObjectMetadataAsync_RejectsEmptyMemoryId()
    {
        var service = CreateService(new FakeHttpMessageHandler(
            (_, _) => throw new InvalidOperationException(
                "HTTP request should not be sent.")));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetObjectMetadataAsync(
                UserId,
                Guid.Empty,
                "audio/mpeg"));
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("audio/flac")]
    [InlineData("")]
    public async Task GetObjectMetadataAsync_RejectsUnsupportedContentType(
        string contentType)
    {
        var service = CreateService(new FakeHttpMessageHandler(
            (_, _) => throw new InvalidOperationException(
                "HTTP request should not be sent.")));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetObjectMetadataAsync(
                UserId,
                MemoryId,
                contentType));
    }

    [Fact]
    public async Task GetObjectMetadataAsync_ThrowsWhenSupabaseReturnsError()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(
                HttpStatusCode.InternalServerError)));

        var service = CreateService(handler);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.GetObjectMetadataAsync(
                UserId,
                MemoryId,
                "audio/mpeg"));

        Assert.Contains("500", exception.Message);
    }

    [Fact]
    public async Task GetObjectMetadataAsync_RejectsUnexpectedObjectPath()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                {
                    "name": "another-user/another-memory.mp3",
                    "metadata": {
                        "size": 1024000,
                        "mimetype": "audio/mpeg"
                    }
                }
                """)
            }));

        var service = CreateService(handler);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetObjectMetadataAsync(
                UserId,
                MemoryId,
                "audio/mpeg"));
    }

    [Fact]
    public async Task GetObjectMetadataAsync_RejectsMissingMetadata()
    {
        // Arrange
        var expectedPath = $"{UserId:D}/{MemoryId:D}.mp3";

        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""
                {
                    "name": "{{expectedPath}}"
                }
                """)
            }));

        var service = CreateService(handler);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetObjectMetadataAsync(
                UserId,
                MemoryId,
                "audio/mpeg"));
    }

    [Fact]
    public async Task GetObjectMetadataAsync_RejectsInvalidObjectSize()
    {
        // Arrange
        var expectedPath = $"{UserId:D}/{MemoryId:D}.mp3";

        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""
                {
                    "name": "{{expectedPath}}",
                    "metadata": {
                        "size": "invalid",
                        "mimetype": "audio/mpeg"
                    }
                }
                """)
            }));

        var service = CreateService(handler);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetObjectMetadataAsync(
                UserId,
                MemoryId,
                "audio/mpeg"));
    }


    [Fact]
    public async Task GetObjectMetadataAsync_RejectsNegativeObjectSize()
    {
        var expectedPath = $"{UserId:D}/{MemoryId:D}.mp3";

        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""
                {
                    "name": "{{expectedPath}}",
                    "metadata": {
                        "size": -1,
                        "mimetype": "audio/mpeg"
                    }
                }
                """)
            }));

        var service = CreateService(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetObjectMetadataAsync(
                UserId,
                MemoryId,
                "audio/mpeg"));
    }

    [Fact]
    public async Task GetObjectMetadataAsync_RejectsMissingMimeType()
    {
        var expectedPath = $"{UserId:D}/{MemoryId:D}.mp3";

        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""
                {
                    "name": "{{expectedPath}}",
                    "metadata": {
                        "size": 1024
                    }
                }
                """)
            }));

        var service = CreateService(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetObjectMetadataAsync(
                UserId,
                MemoryId,
                "audio/mpeg"));
    }

    [Fact]
    public async Task GetObjectMetadataAsync_RejectsEmptyMimeType()
    {
        var expectedPath = $"{UserId:D}/{MemoryId:D}.mp3";

        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""
                {
                    "name": "{{expectedPath}}",
                    "metadata": {
                        "size": 1024,
                        "mimetype": ""
                    }
                }
                """)
            }));

        var service = CreateService(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetObjectMetadataAsync(
                UserId,
                MemoryId,
                "audio/mpeg"));
    }

    [Fact]
    public async Task GetObjectMetadataAsync_RejectsUnexpectedMimeType()
    {
        var expectedPath = $"{UserId:D}/{MemoryId:D}.mp3";

        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""
                {
                    "name": "{{expectedPath}}",
                    "metadata": {
                        "size": 1024,
                        "mimetype": "audio/wav"
                    }
                }
                """)
            }));

        var service = CreateService(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetObjectMetadataAsync(
                UserId,
                MemoryId,
                "audio/mpeg"));
    }

    private sealed class FakeHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return handler(request, cancellationToken);
        }
    }
}
