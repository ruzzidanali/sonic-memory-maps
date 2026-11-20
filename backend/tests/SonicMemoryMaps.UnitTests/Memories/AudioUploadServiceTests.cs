using SonicMemoryMaps.Application.Abstractions;
using SonicMemoryMaps.Application.Memories;

namespace SonicMemoryMaps.UnitTests.Memories;

public sealed class AudioUploadServiceTests
{
    private static readonly Guid TestUserId =
        Guid.Parse("013a0793-1af6-4b10-8ee1-7bdc2c737eac");

    [Fact]
    public async Task CreateUploadAuthorizationAsync_WithValidCommand_ReturnsSuccess()
    {
        var storage = new FakeAudioStorageService();
        var service = new AudioUploadService(
            new FakeCurrentUser(TestUserId, true),
            storage);

        var result = await service.CreateUploadAuthorizationAsync(
            new CreateAudioUploadCommand("audio/mp4"));

        Assert.Equal(AudioUploadServiceStatus.Success, result.Status);
        Assert.NotNull(result.Data);
        Assert.NotEqual(Guid.Empty, result.Data.MemoryId);
        Assert.Equal(1, storage.CreateAuthorizationCallCount);
    }

    [Fact]
    public async Task CreateUploadAuthorizationAsync_GeneratesUniqueMemoryIds()
    {
        var storage = new FakeAudioStorageService();
        var service = new AudioUploadService(
            new FakeCurrentUser(TestUserId, true),
            storage);

        var first = await service.CreateUploadAuthorizationAsync(
            new CreateAudioUploadCommand("audio/mp4"));

        var second = await service.CreateUploadAuthorizationAsync(
            new CreateAudioUploadCommand("audio/mp4"));

        Assert.NotEqual(first.Data!.MemoryId, second.Data!.MemoryId);
    }

    [Fact]
    public async Task CreateUploadAuthorizationAsync_PassesAuthenticatedUserIdToStorage()
    {
        var storage = new FakeAudioStorageService();
        var service = new AudioUploadService(
            new FakeCurrentUser(TestUserId, true),
            storage);

        await service.CreateUploadAuthorizationAsync(
            new CreateAudioUploadCommand("audio/mp4"));

        Assert.Equal(TestUserId, storage.LastUserId);
    }

    [Fact]
    public async Task CreateUploadAuthorizationAsync_WithUnauthenticatedUser_ReturnsUnauthorized()
    {
        var storage = new FakeAudioStorageService();
        var service = new AudioUploadService(
            new FakeCurrentUser(TestUserId, false),
            storage);

        var result = await service.CreateUploadAuthorizationAsync(
            new CreateAudioUploadCommand("audio/mp4"));

        Assert.Equal(AudioUploadServiceStatus.Unauthorized, result.Status);
        Assert.Equal(0, storage.CreateAuthorizationCallCount);
    }

    [Fact]
    public async Task CreateUploadAuthorizationAsync_WithoutUserId_ReturnsUnauthorized()
    {
        var storage = new FakeAudioStorageService();
        var service = new AudioUploadService(
            new FakeCurrentUser(null, true),
            storage);

        var result = await service.CreateUploadAuthorizationAsync(
            new CreateAudioUploadCommand("audio/mp4"));

        Assert.Equal(AudioUploadServiceStatus.Unauthorized, result.Status);
        Assert.Equal(0, storage.CreateAuthorizationCallCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("application/json")]
    [InlineData("audio/flac")]
    [InlineData("video/mp4")]
    public async Task CreateUploadAuthorizationAsync_WithUnsupportedContentType_ReturnsInvalid(
        string contentType)
    {
        var storage = new FakeAudioStorageService();
        var service = new AudioUploadService(
            new FakeCurrentUser(TestUserId, true),
            storage);

        var result = await service.CreateUploadAuthorizationAsync(
            new CreateAudioUploadCommand(contentType));

        Assert.Equal(AudioUploadServiceStatus.Invalid, result.Status);
        Assert.Equal(0, storage.CreateAuthorizationCallCount);
    }

    private sealed class FakeCurrentUser(
        Guid? userId,
        bool isAuthenticated) : ICurrentUser
    {
        public Guid? UserId => userId;
        public string? Email => "test@example.com";
        public bool IsAuthenticated => isAuthenticated;
    }

    private sealed class FakeAudioStorageService : IAudioStorageService
    {
        public int CreateAuthorizationCallCount { get; private set; }
        public Guid? LastUserId { get; private set; }

        public Task<AudioUploadAuthorization> CreateUploadAuthorizationAsync(
            Guid userId,
            Guid memoryId,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            CreateAuthorizationCallCount++;
            LastUserId = userId;

            return Task.FromResult(
                new AudioUploadAuthorization(
                    $"{userId:D}/{memoryId:D}.m4a",
                    "https://test-project.supabase.co/upload/signed-url",
                    DateTimeOffset.UtcNow.AddHours(2)));
        }

        public Task<AudioObjectMetadata?> GetObjectMetadataAsync(
            Guid userId,
            Guid memoryId,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<AudioObjectMetadata?>(null);
        }

        public Task<AudioDownloadAuthorization> CreateDownloadAuthorizationAsync(
            Guid userId,
            Guid memoryId,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new AudioDownloadAuthorization(
                    "https://test-project.supabase.co/storage/v1/object/sign/test",
                    DateTimeOffset.UtcNow.AddMinutes(5)));
        }
    }
}