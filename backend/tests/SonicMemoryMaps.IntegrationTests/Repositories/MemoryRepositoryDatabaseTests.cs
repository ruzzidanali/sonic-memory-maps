
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NetTopologySuite.Geometries;
using SonicMemoryMaps.Domain.Entities;
using SonicMemoryMaps.Domain.Enums;
using SonicMemoryMaps.Infrastructure.Persistence;
using SonicMemoryMaps.Infrastructure.Persistence.Repositories;

namespace SonicMemoryMaps.IntegrationTests.Repositories;

[Trait("Category", "Database")]
public sealed class MemoryRepositoryDatabaseTests
{
    private const string RestrictedConnectionVariable =
        "SONICMEMORYMAPS_RLS_TEST_CONNECTION_STRING";

    private const string AdminConnectionVariable =
        "SONICMEMORYMAPS_RLS_TEST_ADMIN_CONNECTION_STRING";

    private const string UserAVariable =
        "SONICMEMORYMAPS_RLS_TEST_USER_A_ID";

    private const string UserBVariable =
        "SONICMEMORYMAPS_RLS_TEST_USER_B_ID";

    [AdminDatabaseFact]
    public async Task MemoryRepository_EnforcesOwnershipAndPersistsChanges()
    {
        var restrictedConnectionString =
            Environment.GetEnvironmentVariable(RestrictedConnectionVariable);

        var adminConnectionValue =
            Environment.GetEnvironmentVariable(AdminConnectionVariable);

        var userAValue =
            Environment.GetEnvironmentVariable(UserAVariable);

        var userBValue =
            Environment.GetEnvironmentVariable(UserBVariable);

        Assert.False(string.IsNullOrWhiteSpace(restrictedConnectionString));
        Assert.False(string.IsNullOrWhiteSpace(adminConnectionValue));
        Assert.True(Guid.TryParse(userAValue, out var userAId));
        Assert.True(Guid.TryParse(userBValue, out var userBId));
        Assert.NotEqual(userAId, userBId);

        var adminConnectionString =
            CreateAdminConnectionString(adminConnectionValue!);

        var restrictedOptions =
            CreateDbContextOptions(restrictedConnectionString!);

        var adminOptions =
            CreateDbContextOptions(adminConnectionString);

        var olderOwnerMemory = CreateMemory(
            userAId,
            DateTimeOffset.UtcNow.AddMinutes(-10));

        var deletedOwnerMemory = CreateMemory(
            userAId,
            DateTimeOffset.UtcNow,
            deleted: true);

        var publicMemoryOfUserB = CreateMemory(
            userBId,
            DateTimeOffset.UtcNow);

        var seededMemoryIds = new[]
        {
            olderOwnerMemory.Id,
            deletedOwnerMemory.Id,
            publicMemoryOfUserB.Id
        };

        var addedMemory = CreateMemory(
            userAId,
            DateTimeOffset.UtcNow);

        var testMemoryIds = seededMemoryIds
            .Append(addedMemory.Id)
            .ToArray();

        await using var adminContext =
            new SonicMemoryMapsDbContext(adminOptions);

        try
        {
            // Seed records using the administrative connection.
            adminContext.Memories.AddRange(
                olderOwnerMemory,
                deletedOwnerMemory,
                publicMemoryOfUserB);

            await adminContext.SaveChangesAsync();

            await using var restrictedContext =
                new SonicMemoryMapsDbContext(restrictedOptions);

            var repository = new MemoryRepository(restrictedContext);

            // 1. Verify that the restricted connection uses sonic_api.
            await restrictedContext.Database.OpenConnectionAsync();

            try
            {
                await using var command =
                    restrictedContext.Database
                        .GetDbConnection()
                        .CreateCommand();

                command.CommandText = "SELECT current_user";

                var currentRole =
                    (string?)await command.ExecuteScalarAsync();

                Assert.Equal("sonic_api", currentRole);
            }
            finally
            {
                await restrictedContext.Database.CloseConnectionAsync();
            }

            // 2. A user cannot create a memory for another user.
            var invalidMemory = CreateMemory(userBId, DateTimeOffset.UtcNow);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.AddAsync(invalidMemory, userAId));

            // 3. The owner can create their own memory.
            await repository.AddAsync(addedMemory, userAId);

            // 4. The owner can retrieve their own active memory.
            var retrievedMemory =
                await repository.GetByIdForOwnerAsync(
                    addedMemory.Id,
                    userAId);

            Assert.NotNull(retrievedMemory);
            Assert.Equal(userAId, retrievedMemory.UserId);

            // 5. A user cannot retrieve another user's memory through
            // the owner-specific repository method, even if it is public.
            var otherUserMemory =
                await repository.GetByIdForOwnerAsync(
                    publicMemoryOfUserB.Id,
                    userAId);

            Assert.Null(otherUserMemory);

            // 6. Deleted memories are excluded from owner retrieval.
            var deletedMemory =
                await repository.GetByIdForOwnerAsync(
                    deletedOwnerMemory.Id,
                    userAId);

            Assert.Null(deletedMemory);

            // 7. Verify tracked retrieval for the owner.
            var trackedMemory =
                await repository.GetTrackedByIdForOwnerAsync(
                    addedMemory.Id,
                    userAId);

            Assert.NotNull(trackedMemory);

            trackedMemory.Title = "Updated PostgreSQL test memory";
            trackedMemory.UpdatedAt = DateTimeOffset.UtcNow;

            await repository.SaveChangesAsync(userAId);

            // 8. Verify persisted changes using a fresh DbContext.
            await using var verificationContext =
                new SonicMemoryMapsDbContext(restrictedOptions);

            var verificationRepository =
                new MemoryRepository(verificationContext);

            var updatedMemory =
                await verificationRepository.GetByIdForOwnerAsync(
                    addedMemory.Id,
                    userAId);

            Assert.NotNull(updatedMemory);
            Assert.Equal(
                "Updated PostgreSQL test memory",
                updatedMemory.Title);

            // 9. Verify GetByOwnerAsync returns only the owner's
            // active memories, not public memories belonging to others.
            var ownerMemories =
                await verificationRepository.GetByOwnerAsync(userAId);

            // Both test-created active memories must be returned.
            Assert.Contains(
                ownerMemories,
                memory => memory.Id == addedMemory.Id);

            Assert.Contains(
                ownerMemories,
                memory => memory.Id == olderOwnerMemory.Id);

            // No other user's memories should be returned.
            Assert.DoesNotContain(
                ownerMemories,
                memory => memory.UserId != userAId);

            // Deleted memories must be excluded.
            Assert.DoesNotContain(
                ownerMemories,
                memory => memory.DeletedAt is not null);

            // Public memories belonging to User B must not appear.
            Assert.DoesNotContain(
                ownerMemories,
                memory => memory.Id == publicMemoryOfUserB.Id);

            // The newly created memory must appear before the older test memory.
            var addedMemoryIndex = ownerMemories.ToList()
                .FindIndex(memory => memory.Id == addedMemory.Id);

            var olderMemoryIndex = ownerMemories.ToList()
                .FindIndex(memory => memory.Id == olderOwnerMemory.Id);

            Assert.True(addedMemoryIndex < olderMemoryIndex);

            // 10. Verify that SaveChangesAsync rejects another user's
            // memory even if the entity is attached to this DbContext.
            await using var ownershipContext =
                new SonicMemoryMapsDbContext(restrictedOptions);

            var otherUserMemoryForUpdate =
                await adminContext.Memories
                    .AsNoTracking()
                    .FirstAsync(
                        memory => memory.Id == publicMemoryOfUserB.Id);

            ownershipContext.Memories.Attach(otherUserMemoryForUpdate);

            otherUserMemoryForUpdate.Title =
                "Attempted unauthorized update";

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => new MemoryRepository(ownershipContext)
                    .SaveChangesAsync(userAId));
        }
        finally
        {
            // Remove only records created by this test.
            adminContext.ChangeTracker.Clear();

            await adminContext.Memories
                .Where(memory => testMemoryIds.Contains(memory.Id))
                .ExecuteDeleteAsync();
        }
    }

    private static DbContextOptions<SonicMemoryMapsDbContext>
        CreateDbContextOptions(string connectionString)
    {
        return new DbContextOptionsBuilder<SonicMemoryMapsDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.UseNetTopologySuite())
            .Options;
    }

    private static string CreateAdminConnectionString(string connectionUri)
    {
        var uri = new Uri(connectionUri);
        var userInfo = uri.UserInfo.Split(':', 2);

        if (userInfo.Length != 2)
        {
            throw new InvalidOperationException(
                "Invalid administrative database connection URI.");
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = Uri.UnescapeDataString(userInfo[1]),
            SslMode = SslMode.Require
        };

        return builder.ConnectionString;
    }

    private static Memory CreateMemory(
        Guid userId,
        DateTimeOffset createdAt,
        bool deleted = false)
    {
        var memoryId = Guid.NewGuid();

        return new Memory
        {
            Id = memoryId,
            UserId = userId,
            Title = $"Repository Test {memoryId:N}",
            Description = "Temporary repository integration test.",
            AudioPath = $"{userId}/{memoryId}.m4a",
            AudioDurationSeconds = 30,
            AudioContentType = "audio/mp4",
            AudioSizeBytes = 1024,
            Location = new Point(101.6869, 3.1390)
            {
                SRID = 4326
            },
            Visibility = MemoryVisibility.Public,
            NotificationRadiusMeters = 50,
            ModerationStatus = ModerationStatus.Approved,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            DeletedAt = deleted ? createdAt : null
        };
    }
}
