
using SonicMemoryMaps.Application.Abstractions;
using SonicMemoryMaps.Application.Memories;
using SonicMemoryMaps.Domain.Entities;
using SonicMemoryMaps.Domain.Enums;

namespace SonicMemoryMaps.UnitTests.Memories;

public sealed class MemoryServiceTests
{
    private static readonly Guid TestUserId =
        Guid.Parse("013a0793-1af6-4b10-8ee1-7bdc2c737eac");

    private static readonly Guid TestMemoryId =
    Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task CreateMemoryAsync_WithValidCommand_AssignsAuthenticatedUserAsOwner()
    {
        var repository = new FakeMemoryRepository();
        var service = new MemoryService(
            repository,
            new FakeCurrentUser(TestUserId, true),
            new FakeAudioStorageService());

        await service.CreateMemoryAsync(CreateValidCommand());

        Assert.NotNull(repository.AddedMemory);
        Assert.Equal(TestUserId, repository.AddedMemory.UserId);
    }

    [Fact]
    public async Task CreateMemoryAsync_WithValidCommand_CreatesCorrectLocation()
    {
        var repository = new FakeMemoryRepository();
        var service = new MemoryService(
            repository,
            new FakeCurrentUser(TestUserId, true),
            new FakeAudioStorageService());

        await service.CreateMemoryAsync(CreateValidCommand());

        var location = Assert.IsType<NetTopologySuite.Geometries.Point>(
            repository.AddedMemory!.Location);

        Assert.Equal(101.6869, location.X);
        Assert.Equal(3.1390, location.Y);
        Assert.Equal(4326, location.SRID);
    }

    [Fact]
    public async Task CreateMemoryAsync_WithValidCommand_SetsPendingModeration()
    {
        var repository = new FakeMemoryRepository();
        var service = new MemoryService(
            repository,
            new FakeCurrentUser(TestUserId, true),
            new FakeAudioStorageService());

        await service.CreateMemoryAsync(CreateValidCommand());

        Assert.Equal(
            ModerationStatus.Pending,
            repository.AddedMemory!.ModerationStatus);
    }

    [Fact]
    public async Task CreateMemoryAsync_WithUnauthenticatedUser_ReturnsUnauthorized()
    {
        var repository = new FakeMemoryRepository();
        var service = new MemoryService(
            repository,
            new FakeCurrentUser(TestUserId, false),
            new FakeAudioStorageService());

        var result = await service.CreateMemoryAsync(CreateValidCommand());

        Assert.Equal(MemoryServiceStatus.Unauthorized, result.Status);
        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public async Task CreateMemoryAsync_WithoutUserId_ReturnsUnauthorized()
    {
        var repository = new FakeMemoryRepository();
        var service = new MemoryService(
            repository,
            new FakeCurrentUser(null, true),
            new FakeAudioStorageService());

        var result = await service.CreateMemoryAsync(CreateValidCommand());

        Assert.Equal(MemoryServiceStatus.Unauthorized, result.Status);
        Assert.Equal(0, repository.AddCallCount);
    }

    [Theory]
    [MemberData(nameof(InvalidCommands))]
    public async Task CreateMemoryAsync_WithInvalidCommand_ReturnsInvalid(
        CreateMemoryCommand command)
    {
        var repository = new FakeMemoryRepository();
        var service = new MemoryService(
            repository,
            new FakeCurrentUser(TestUserId, true),
            new FakeAudioStorageService());

        var result = await service.CreateMemoryAsync(command);

        Assert.Equal(MemoryServiceStatus.Invalid, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public async Task CreateMemoryAsync_WhenAudioObjectDoesNotExist_ReturnsNotFound()
    {
        var repository = new FakeMemoryRepository();
        var storage = new FakeAudioStorageService { Metadata = null };

        var service = new MemoryService(
            repository,
            new FakeCurrentUser(TestUserId, true),
            storage);

        var result = await service.CreateMemoryAsync(CreateValidCommand());

        Assert.Equal(MemoryServiceStatus.NotFound, result.Status);
        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public async Task CreateMemoryAsync_WhenAudioExceedsMaximumSize_ReturnsInvalid()
    {
        var repository = new FakeMemoryRepository();
        var storage = new FakeAudioStorageService
        {
            Metadata = new AudioObjectMetadata(
                $"{TestUserId:D}/{TestMemoryId:D}.m4a",
                "audio/mp4",
                52_428_801)
        };

        var service = new MemoryService(
            repository,
            new FakeCurrentUser(TestUserId, true),
            storage);

        var result = await service.CreateMemoryAsync(CreateValidCommand());

        Assert.Equal(MemoryServiceStatus.Invalid, result.Status);
        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public async Task CreateMemoryAsync_WhenAudioIsEmpty_ReturnsInvalid()
    {
        var repository = new FakeMemoryRepository();
        var storage = new FakeAudioStorageService
        {
            Metadata = new AudioObjectMetadata(
                $"{TestUserId:D}/{TestMemoryId:D}.m4a",
                "audio/mp4",
                0)
        };

        var service = new MemoryService(
            repository,
            new FakeCurrentUser(TestUserId, true),
            storage);

        var result = await service.CreateMemoryAsync(CreateValidCommand());

        Assert.Equal(MemoryServiceStatus.Invalid, result.Status);
        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public async Task CreateMemoryAsync_WhenAudioContentTypeDoesNotMatch_ReturnsInvalid()
    {
        var repository = new FakeMemoryRepository();
        var storage = new FakeAudioStorageService
        {
            Metadata = new AudioObjectMetadata(
                $"{TestUserId:D}/{TestMemoryId:D}.m4a",
                "audio/mpeg",
                2_000_000)
        };

        var service = new MemoryService(
            repository,
            new FakeCurrentUser(TestUserId, true),
            storage);

        var result = await service.CreateMemoryAsync(CreateValidCommand());

        Assert.Equal(MemoryServiceStatus.Invalid, result.Status);
        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public async Task CreateMemoryAsync_WithValidAudio_UsesVerifiedStorageMetadata()
    {
        var repository = new FakeMemoryRepository();

        var storage = new FakeAudioStorageService
        {
            Metadata = new AudioObjectMetadata(
                $"{TestUserId:D}/{TestMemoryId:D}.m4a",
                "audio/mp4",
                2_000_000)
        };

        var service = new MemoryService(
            repository,
            new FakeCurrentUser(TestUserId, true),
            storage);

        var result = await service.CreateMemoryAsync(CreateValidCommand());

        Assert.Equal(MemoryServiceStatus.Success, result.Status);

        Assert.NotNull(repository.AddedMemory);

        Assert.Equal(
            $"{TestUserId:D}/{TestMemoryId:D}.m4a",
            repository.AddedMemory.AudioPath);

        Assert.Equal("audio/mp4", repository.AddedMemory.AudioContentType);
        Assert.Equal(2_000_000, repository.AddedMemory.AudioSizeBytes);

        Assert.Equal(1, storage.MetadataCallCount);
        Assert.Equal(1, repository.AddCallCount);
    }


    [Fact]
    public async Task GetMemoryByIdAsync_WithValidMemory_ReturnsSuccess()
    {
        var repository = new FakeMemoryRepository
        {
            ExistingMemory = CreateExistingMemory(TestUserId),
            ExistingMemoryOwnerId = TestUserId
        };

        var service = new MemoryService(
            repository,
            new FakeCurrentUser(TestUserId, true),
            new FakeAudioStorageService());

        var result = await service.GetMemoryByIdAsync(TestMemoryId);

        Assert.Equal(MemoryServiceStatus.Success, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(TestMemoryId, result.Data.Id);
        Assert.Equal(TestUserId, result.Data.UserId);
        Assert.Equal("Memories at Kuala Lumpur", result.Data.Title);
    }

    [Fact]
    public async Task GetMemoryByIdAsync_WithValidMemory_DoesNotExposeAudioPath()
    {
        var repository = new FakeMemoryRepository
        {
            ExistingMemory = CreateExistingMemory(TestUserId),
            ExistingMemoryOwnerId = TestUserId
        };

        var service = new MemoryService(
            repository,
            new FakeCurrentUser(TestUserId, true),
            new FakeAudioStorageService());

        var result = await service.GetMemoryByIdAsync(TestMemoryId);

        Assert.NotNull(result.Data);

        var dtoProperties = typeof(MemoryDto).GetProperties();

        Assert.DoesNotContain(
            dtoProperties,
            property => property.Name == "AudioPath");
    }

    [Fact]
    public async Task GetMemoryByIdAsync_WithUnauthenticatedUser_ReturnsUnauthorized()
    {
        var repository = new FakeMemoryRepository();

        var service = new MemoryService(
            repository,
            new FakeCurrentUser(TestUserId, false),
            new FakeAudioStorageService());

        var result = await service.GetMemoryByIdAsync(TestMemoryId);

        Assert.Equal(MemoryServiceStatus.Unauthorized, result.Status);
    }

    [Fact]
    public async Task GetMemoryByIdAsync_WithoutUserId_ReturnsUnauthorized()
    {
        var repository = new FakeMemoryRepository();

        var service = new MemoryService(
            repository,
            new FakeCurrentUser(null, true),
            new FakeAudioStorageService());

        var result = await service.GetMemoryByIdAsync(TestMemoryId);

        Assert.Equal(MemoryServiceStatus.Unauthorized, result.Status);
    }

    [Fact]
    public async Task GetMemoryByIdAsync_WhenMemoryDoesNotExist_ReturnsNotFound()
    {
        var repository = new FakeMemoryRepository();

        var service = new MemoryService(
            repository,
            new FakeCurrentUser(TestUserId, true),
            new FakeAudioStorageService());

        var result = await service.GetMemoryByIdAsync(TestMemoryId);

        Assert.Equal(MemoryServiceStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task GetMemoryByIdAsync_WhenMemoryBelongsToAnotherUser_ReturnsNotFound()
    {
        var anotherUserId = Guid.NewGuid();

        var repository = new FakeMemoryRepository
        {
            ExistingMemory = CreateExistingMemory(anotherUserId),
            ExistingMemoryOwnerId = anotherUserId
        };

        var service = new MemoryService(
            repository,
            new FakeCurrentUser(TestUserId, true),
            new FakeAudioStorageService());

        var result = await service.GetMemoryByIdAsync(TestMemoryId);

        Assert.Equal(MemoryServiceStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task GetMemoryByIdAsync_WithEmptyMemoryId_ReturnsInvalid()
    {
        var repository = new FakeMemoryRepository();

        var service = new MemoryService(
            repository,
            new FakeCurrentUser(TestUserId, true),
            new FakeAudioStorageService());

        var result = await service.GetMemoryByIdAsync(Guid.Empty);

        Assert.Equal(MemoryServiceStatus.Invalid, result.Status);
    }


    [Fact]
    public async Task UpdateMemoryAsync_WithValidCommand_ReturnsSuccess()
    {
        var existingMemory = CreateExistingMemory(TestUserId);

        var repository = new FakeMemoryRepository
        {
            TrackedMemory = existingMemory
        };

        var service = new MemoryService(
            repository,
            new FakeCurrentUser(TestUserId, true),
            new FakeAudioStorageService());

        var command = new UpdateMemoryCommand(
            TestMemoryId,
            "Updated title",
            "Updated description",
            MemoryVisibility.FriendsOnly,
            100);

        var result = await service.UpdateMemoryAsync(command);

        Assert.Equal(MemoryServiceStatus.Success, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal("Updated title", result.Data.Title);
        Assert.Equal("Updated description", result.Data.Description);
        Assert.Equal(MemoryVisibility.FriendsOnly, result.Data.Visibility);
        Assert.Equal(100, result.Data.NotificationRadiusMeters);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateMemoryAsync_WithValidCommand_PreservesImmutableFields()
    {
        var existingMemory = CreateExistingMemory(TestUserId);

        var originalAudioPath = existingMemory.AudioPath;
        var originalAudioDuration = existingMemory.AudioDurationSeconds;
        var originalAudioContentType = existingMemory.AudioContentType;
        var originalAudioSize = existingMemory.AudioSizeBytes;
        var originalLocation = existingMemory.Location.Copy();
        var originalModerationStatus = existingMemory.ModerationStatus;
        var originalUserId = existingMemory.UserId;

        var repository = new FakeMemoryRepository
        {
            TrackedMemory = existingMemory
        };

        var service = new MemoryService(
            repository,
            new FakeCurrentUser(TestUserId, true),
            new FakeAudioStorageService());

        var command = new UpdateMemoryCommand(
            TestMemoryId,
            "Updated title",
            null,
            MemoryVisibility.Private,
            150);

        await service.UpdateMemoryAsync(command);

        Assert.Equal(originalUserId, existingMemory.UserId);
        Assert.Equal(originalAudioPath, existingMemory.AudioPath);
        Assert.Equal(originalAudioDuration, existingMemory.AudioDurationSeconds);
        Assert.Equal(originalAudioContentType, existingMemory.AudioContentType);
        Assert.Equal(originalAudioSize, existingMemory.AudioSizeBytes);
        Assert.Equal(originalLocation, existingMemory.Location);
        Assert.Equal(originalModerationStatus, existingMemory.ModerationStatus);
    }

    [Fact]
    public async Task UpdateMemoryAsync_WithValidCommand_UpdatesUpdatedAt()
    {
        var existingMemory = CreateExistingMemory(TestUserId);
        var originalUpdatedAt = existingMemory.UpdatedAt;

        var repository = new FakeMemoryRepository
        {
            TrackedMemory = existingMemory
        };

        var service = new MemoryService(
            repository,
            new FakeCurrentUser(TestUserId, true),
            new FakeAudioStorageService());

        await service.UpdateMemoryAsync(
            new UpdateMemoryCommand(
                TestMemoryId,
                "Updated title",
                "Updated description",
                MemoryVisibility.Public,
                75));

        Assert.True(existingMemory.UpdatedAt >= originalUpdatedAt);
    }

    [Fact]
    public async Task UpdateMemoryAsync_WithUnauthenticatedUser_ReturnsUnauthorized()
    {
        var repository = new FakeMemoryRepository();

        var service = new MemoryService(
            repository,
            new FakeCurrentUser(TestUserId, false),
            new FakeAudioStorageService());

        var result = await service.UpdateMemoryAsync(
            new UpdateMemoryCommand(
                TestMemoryId,
                "Updated title",
                null,
                MemoryVisibility.Public,
                50));

        Assert.Equal(MemoryServiceStatus.Unauthorized, result.Status);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateMemoryAsync_WhenMemoryDoesNotExist_ReturnsNotFound()
    {
        var repository = new FakeMemoryRepository();

        var service = new MemoryService(
            repository,
            new FakeCurrentUser(TestUserId, true),
            new FakeAudioStorageService());

        var result = await service.UpdateMemoryAsync(
            new UpdateMemoryCommand(
                TestMemoryId,
                "Updated title",
                null,
                MemoryVisibility.Public,
                50));

        Assert.Equal(MemoryServiceStatus.NotFound, result.Status);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateMemoryAsync_WhenMemoryBelongsToAnotherUser_ReturnsNotFound()
    {
        var anotherUserId = Guid.NewGuid();

        var repository = new FakeMemoryRepository
        {
            TrackedMemory = CreateExistingMemory(anotherUserId)
        };

        var service = new MemoryService(
            repository,
            new FakeCurrentUser(TestUserId, true),
            new FakeAudioStorageService());

        var result = await service.UpdateMemoryAsync(
            new UpdateMemoryCommand(
                TestMemoryId,
                "Updated title",
                null,
                MemoryVisibility.Public,
                50));

        Assert.Equal(MemoryServiceStatus.NotFound, result.Status);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateMemoryAsync_WithInvalidCommand_ReturnsInvalid()
    {
        var repository = new FakeMemoryRepository
        {
            TrackedMemory = CreateExistingMemory(TestUserId)
        };

        var service = new MemoryService(
            repository,
            new FakeCurrentUser(TestUserId, true),
            new FakeAudioStorageService());

        var command = new UpdateMemoryCommand(
            TestMemoryId,
            "",
            null,
            MemoryVisibility.Public,
            50);

        var result = await service.UpdateMemoryAsync(command);

        Assert.Equal(MemoryServiceStatus.Invalid, result.Status);
        Assert.Equal(0, repository.GetTrackedCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }


    [Fact]
    public async Task DeleteMemoryAsync_WithValidMemory_ReturnsSuccess()
    {
        var memory = CreateExistingMemory(TestUserId);

        var repository = new FakeMemoryRepository
        {
            TrackedMemory = memory
        };

        var service = new MemoryService(
            repository,
            new FakeCurrentUser(TestUserId, true),
            new FakeAudioStorageService());

        var result = await service.DeleteMemoryAsync(TestMemoryId);

        Assert.Equal(MemoryServiceStatus.Success, result.Status);
        Assert.True(result.Data);
        Assert.NotNull(memory.DeletedAt);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task DeleteMemoryAsync_WithValidMemory_UpdatesUpdatedAt()
    {
        var memory = CreateExistingMemory(TestUserId);
        var originalUpdatedAt = memory.UpdatedAt;

        var repository = new FakeMemoryRepository
        {
            TrackedMemory = memory
        };

        var service = new MemoryService(
            repository,
            new FakeCurrentUser(TestUserId, true),
            new FakeAudioStorageService());

        await service.DeleteMemoryAsync(TestMemoryId);

        Assert.NotNull(memory.DeletedAt);
        Assert.True(memory.UpdatedAt >= originalUpdatedAt);
        Assert.Equal(memory.DeletedAt, memory.UpdatedAt);
    }

    [Fact]
    public async Task DeleteMemoryAsync_WithUnauthenticatedUser_ReturnsUnauthorized()
    {
        var repository = new FakeMemoryRepository();

        var service = new MemoryService(
            repository,
            new FakeCurrentUser(TestUserId, false),
            new FakeAudioStorageService());

        var result = await service.DeleteMemoryAsync(TestMemoryId);

        Assert.Equal(MemoryServiceStatus.Unauthorized, result.Status);
        Assert.Equal(0, repository.GetTrackedCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task DeleteMemoryAsync_WithoutUserId_ReturnsUnauthorized()
    {
        var repository = new FakeMemoryRepository();

        var service = new MemoryService(
            repository,
            new FakeCurrentUser(null, true),
            new FakeAudioStorageService());

        var result = await service.DeleteMemoryAsync(TestMemoryId);

        Assert.Equal(MemoryServiceStatus.Unauthorized, result.Status);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task DeleteMemoryAsync_WhenMemoryDoesNotExist_ReturnsNotFound()
    {
        var repository = new FakeMemoryRepository();

        var service = new MemoryService(
            repository,
            new FakeCurrentUser(TestUserId, true),
            new FakeAudioStorageService());

        var result = await service.DeleteMemoryAsync(TestMemoryId);

        Assert.Equal(MemoryServiceStatus.NotFound, result.Status);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task DeleteMemoryAsync_WhenMemoryBelongsToAnotherUser_ReturnsNotFound()
    {
        var anotherUserId = Guid.NewGuid();

        var repository = new FakeMemoryRepository
        {
            TrackedMemory = CreateExistingMemory(anotherUserId)
        };

        var service = new MemoryService(
            repository,
            new FakeCurrentUser(TestUserId, true),
            new FakeAudioStorageService());

        var result = await service.DeleteMemoryAsync(TestMemoryId);

        Assert.Equal(MemoryServiceStatus.NotFound, result.Status);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task DeleteMemoryAsync_WithEmptyMemoryId_ReturnsInvalid()
    {
        var repository = new FakeMemoryRepository();

        var service = new MemoryService(
            repository,
            new FakeCurrentUser(TestUserId, true),
            new FakeAudioStorageService());

        var result = await service.DeleteMemoryAsync(Guid.Empty);

        Assert.Equal(MemoryServiceStatus.Invalid, result.Status);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    public static IEnumerable<object[]> InvalidCommands()
    {
        var valid = CreateValidCommand();

        yield return [valid with { Title = "" }];
        yield return [valid with { Title = "   " }];
        yield return [valid with { Title = new string('a', 101) }];
        yield return [valid with { Description = new string('a', 501) }];
        yield return [valid with { AudioDurationSeconds = 0 }];
        yield return [valid with { AudioDurationSeconds = 601 }];
        yield return [valid with { Latitude = 91 }];
        yield return [valid with { Latitude = -91 }];
        yield return [valid with { Latitude = double.NaN }];
        yield return [valid with { Longitude = 181 }];
        yield return [valid with { Longitude = -181 }];
        yield return [valid with { Longitude = double.PositiveInfinity }];
        yield return [valid with { NotificationRadiusMeters = 9 }];
        yield return [valid with { NotificationRadiusMeters = 251 }];
        yield return [valid with { Visibility = (MemoryVisibility)999 }];
        yield return [valid with { MemoryId = Guid.Empty }];
        yield return [valid with { RequestedAudioContentType = "" }];
        yield return [valid with { RequestedAudioContentType = "audio/flac" }];
    }

    private static CreateMemoryCommand CreateValidCommand()
    {
        return new CreateMemoryCommand(
            MemoryId: TestMemoryId,
            Title: "  Memories at Kuala Lumpur  ",
            Description: "  A beautiful evening.  ",
            RequestedAudioContentType: "audio/mp4",
            AudioDurationSeconds: 120,
            Latitude: 3.1390,
            Longitude: 101.6869,
            Visibility: MemoryVisibility.Public,
            NotificationRadiusMeters: 50);
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
        public AudioObjectMetadata? Metadata { get; set; } =
            new AudioObjectMetadata(
                $"{TestUserId:D}/{TestMemoryId:D}.m4a",
                "audio/mp4",
                2_000_000);

        public int MetadataCallCount { get; private set; }

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
            MetadataCallCount++;

            return Task.FromResult(Metadata);
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

    private sealed class FakeMemoryRepository : IMemoryRepository
    {
        public Memory? AddedMemory { get; private set; }

        public int AddCallCount { get; private set; }

        public Memory? ExistingMemory { get; set; }

        public Guid? ExistingMemoryOwnerId { get; set; }

        public Memory? TrackedMemory { get; set; }

        public int GetTrackedCallCount { get; private set; }

        public int SaveChangesCallCount { get; private set; }

        public Task AddAsync(
            Memory memory,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            AddCallCount++;
            AddedMemory = memory;

            return Task.CompletedTask;
        }

        public Task<Memory?> GetByIdForOwnerAsync(
            Guid memoryId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (ExistingMemory is null ||
                ExistingMemory.Id != memoryId ||
                ExistingMemoryOwnerId != userId)
            {
                return Task.FromResult<Memory?>(null);
            }

            return Task.FromResult<Memory?>(ExistingMemory);
        }

        public Task<Memory?> GetTrackedByIdForOwnerAsync(
    Guid memoryId,
    Guid userId,
    CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            GetTrackedCallCount++;

            if (TrackedMemory is null ||
                TrackedMemory.Id != memoryId ||
                TrackedMemory.UserId != userId ||
                TrackedMemory.DeletedAt is not null)
            {
                return Task.FromResult<Memory?>(null);
            }

            return Task.FromResult<Memory?>(TrackedMemory);
        }

        public Task<IReadOnlyList<Memory>> GetByOwnerAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Memory>>(
                Array.Empty<Memory>());
        }

        public Task SaveChangesAsync(
    Guid userId,
    CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SaveChangesCallCount++;

            return Task.CompletedTask;
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
