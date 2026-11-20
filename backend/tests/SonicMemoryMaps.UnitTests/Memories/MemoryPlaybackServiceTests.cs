
using SonicMemoryMaps.Application.Abstractions;
using SonicMemoryMaps.Application.Memories;
using SonicMemoryMaps.Domain.Entities;
using SonicMemoryMaps.Domain.Enums;

namespace SonicMemoryMaps.UnitTests.Memories;

public sealed class MemoryPlaybackServiceTests
{
    private static readonly Guid TestUserId =
        Guid.Parse("013a0793-1af6-4b10-8ee1-7bdc2c737eac");

    private static readonly Guid TestMemoryId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task CreatePlaybackAuthorizationAsync_WithUnauthenticatedUser_ReturnsUnauthorized()
    {
        var repository = new FakeMemoryAccessRepository();
        var storage = new FakeAudioStorageService();

        var service = CreateService(
            new FakeCurrentUser(TestUserId, false),
            repository,
            storage);

        var result = await service.CreatePlaybackAuthorizationAsync(TestMemoryId);

        Assert.Equal(MemoryPlaybackServiceStatus.Unauthorized, result.Status);
        Assert.Equal(0, repository.GetCallCount);
        Assert.Equal(0, storage.DownloadCallCount);
    }

    [Fact]
    public async Task CreatePlaybackAuthorizationAsync_WithoutUserId_ReturnsUnauthorized()
    {
        var repository = new FakeMemoryAccessRepository();
        var storage = new FakeAudioStorageService();

        var service = CreateService(
            new FakeCurrentUser(null, true),
            repository,
            storage);

        var result = await service.CreatePlaybackAuthorizationAsync(TestMemoryId);

        Assert.Equal(MemoryPlaybackServiceStatus.Unauthorized, result.Status);
        Assert.Equal(0, repository.GetCallCount);
        Assert.Equal(0, storage.DownloadCallCount);
    }

    [Fact]
    public async Task CreatePlaybackAuthorizationAsync_WithEmptyMemoryId_ReturnsNotFound()
    {
        var repository = new FakeMemoryAccessRepository();
        var storage = new FakeAudioStorageService();

        var service = CreateService(
            new FakeCurrentUser(TestUserId, true),
            repository,
            storage);

        var result = await service.CreatePlaybackAuthorizationAsync(Guid.Empty);

        Assert.Equal(MemoryPlaybackServiceStatus.NotFound, result.Status);
        Assert.Equal(0, repository.GetCallCount);
        Assert.Equal(0, storage.DownloadCallCount);
    }

    [Fact]
    public async Task CreatePlaybackAuthorizationAsync_WhenAccessIsDenied_ReturnsNotFound()
    {
        var repository = new FakeMemoryAccessRepository
        {
            MemoryToReturn = null
        };

        var storage = new FakeAudioStorageService();

        var service = CreateService(
            new FakeCurrentUser(TestUserId, true),
            repository,
            storage);

        var result = await service.CreatePlaybackAuthorizationAsync(TestMemoryId);

        Assert.Equal(MemoryPlaybackServiceStatus.NotFound, result.Status);
        Assert.Equal(1, repository.GetCallCount);
        Assert.Equal(0, storage.DownloadCallCount);
    }

    [Fact]
    public async Task CreatePlaybackAuthorizationAsync_WithAuthorizedMemory_ReturnsPlaybackAuthorization()
    {
        var memory = CreateExistingMemory(TestUserId);

        var repository = new FakeMemoryAccessRepository
        {
            MemoryToReturn = memory
        };

        var storage = new FakeAudioStorageService();

        var service = CreateService(
            new FakeCurrentUser(TestUserId, true),
            repository,
            storage);

        var result = await service.CreatePlaybackAuthorizationAsync(TestMemoryId);

        Assert.Equal(MemoryPlaybackServiceStatus.Success, result.Status);
        Assert.NotNull(result.Data);

        Assert.Equal(TestMemoryId, result.Data.MemoryId);
        Assert.Equal(
            "https://test-project.supabase.co/storage/v1/object/sign/test",
            result.Data.PlaybackUrl);

        Assert.True(result.Data.ExpiresAt > DateTimeOffset.UtcNow);

        Assert.Equal(1, repository.GetCallCount);
        Assert.Equal(1, storage.DownloadCallCount);

        Assert.Equal(TestUserId, repository.LastRequestingUserId);
        Assert.Equal(TestUserId, storage.LastUserId);
        Assert.Equal(TestMemoryId, storage.LastMemoryId);
        Assert.Equal("audio/mp4", storage.LastContentType);
    }

    private static MemoryPlaybackService CreateService(
        ICurrentUser currentUser,
        IMemoryAccessRepository repository,
        IAudioStorageService storage)
    {
        return new MemoryPlaybackService(
            currentUser,
            repository,
            storage);
    }

    private sealed class FakeCurrentUser(
        Guid? userId,
        bool isAuthenticated) : ICurrentUser
    {
        public Guid? UserId => userId;

        public string? Email => "test@example.com";

        public bool IsAuthenticated => isAuthenticated;
    }

    private sealed class FakeMemoryAccessRepository
        : IMemoryAccessRepository
    {
        public Memory? MemoryToReturn { get; set; }

        public int GetCallCount { get; private set; }

        public Guid? LastRequestingUserId { get; private set; }

        public Task<Memory?> GetByIdForPlaybackAsync(
            Guid memoryId,
            Guid requestingUserId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            GetCallCount++;
            LastRequestingUserId = requestingUserId;

            if (MemoryToReturn?.Id != memoryId)
            {
                return Task.FromResult<Memory?>(null);
            }

            return Task.FromResult<Memory?>(MemoryToReturn);
        }
    }

    private sealed class FakeAudioStorageService : IAudioStorageService
    {
        public int DownloadCallCount { get; private set; }

        public Guid? LastUserId { get; private set; }

        public Guid? LastMemoryId { get; private set; }

        public string? LastContentType { get; private set; }

        public Task<AudioUploadAuthorization> CreateUploadAuthorizationAsync(
            Guid userId,
            Guid memoryId,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<AudioObjectMetadata?> GetObjectMetadataAsync(
            Guid userId,
            Guid memoryId,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<AudioDownloadAuthorization> CreateDownloadAuthorizationAsync(
            Guid userId,
            Guid memoryId,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DownloadCallCount++;
            LastUserId = userId;
            LastMemoryId = memoryId;
            LastContentType = contentType;

            return Task.FromResult(
                new AudioDownloadAuthorization(
                    "https://test-project.supabase.co/storage/v1/object/sign/test",
                    DateTimeOffset.UtcNow.AddMinutes(5)));
        }
    }

    private static Memory CreateExistingMemory(Guid userId)
    {
        var now = DateTimeOffset.UtcNow;

        return new Memory
        {
            Id = TestMemoryId,
            UserId = userId,
            Title = "Memories at Kuala Lumpur",
            Description = "A beautiful evening.",
            AudioPath = $"{userId:D}/{TestMemoryId:D}.m4a",
            AudioDurationSeconds = 120,
            AudioContentType = "audio/mp4",
            AudioSizeBytes = 2_000_000,
            Location = new NetTopologySuite.Geometries.Point(
                101.6869,
                3.1390)
            {
                SRID = 4326
            },
            Visibility = MemoryVisibility.Public,
            NotificationRadiusMeters = 50,
            ModerationStatus = ModerationStatus.Approved,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
