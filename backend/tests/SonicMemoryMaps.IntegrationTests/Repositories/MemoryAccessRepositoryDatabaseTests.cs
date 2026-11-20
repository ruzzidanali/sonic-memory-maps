
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Npgsql;
using SonicMemoryMaps.Domain.Entities;
using SonicMemoryMaps.Domain.Enums;
using SonicMemoryMaps.Infrastructure.Persistence;
using SonicMemoryMaps.Infrastructure.Persistence.Repositories;

namespace SonicMemoryMaps.IntegrationTests.Repositories;

[Trait("Category", "Database")]
public sealed class MemoryAccessRepositoryDatabaseTests
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
    public async Task MemoryAccessRepository_EnforcesPlaybackAuthorizationRules()
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

        var memories = new List<Memory>
        {
            CreateMemory(
                userAId,
                MemoryVisibility.Private,
                ModerationStatus.Approved),

            CreateMemory(
                userAId,
                MemoryVisibility.Public,
                ModerationStatus.Pending),

            CreateMemory(
                userAId,
                MemoryVisibility.Public,
                ModerationStatus.Rejected),

            CreateMemory(
                userAId,
                MemoryVisibility.Public,
                ModerationStatus.Approved),

            CreateMemory(
                userAId,
                MemoryVisibility.FriendsOnly,
                ModerationStatus.Approved),

            CreateMemory(
                userBId,
                MemoryVisibility.FriendsOnly,
                ModerationStatus.Approved),

            CreateMemory(
                userAId,
                MemoryVisibility.Private,
                ModerationStatus.Approved),

            CreateMemory(
                userAId,
                MemoryVisibility.Public,
                ModerationStatus.Pending),

            CreateMemory(
                userAId,
                MemoryVisibility.Public,
                ModerationStatus.Rejected),

            CreateMemory(
                userAId,
                MemoryVisibility.Public,
                ModerationStatus.Approved,
                deleted: true)
        };

        var privateMemory = memories[0];
        var pendingOwnerMemory = memories[1];
        var rejectedOwnerMemory = memories[2];
        var publicMemory = memories[3];
        var friendsOnlyMemoryA = memories[4];
        var friendsOnlyMemoryB = memories[5];
        var otherPrivateMemory = memories[6];
        var pendingPublicMemory = memories[7];
        var rejectedPublicMemory = memories[8];
        var deletedMemory = memories[9];

        var friendship = CreateFriendship(userAId, userBId);

        var memoryIds = memories
            .Select(memory => memory.Id)
            .ToArray();

        await using var adminContext =
            new SonicMemoryMapsDbContext(adminOptions);

        await using var restrictedContext =
            new SonicMemoryMapsDbContext(restrictedOptions);

        try
        {
            // Seed temporary records using the administrative connection.
            adminContext.Memories.AddRange(memories);
            await adminContext.SaveChangesAsync();

            var repository =
                new MemoryAccessRepository(restrictedContext);

            // Verify that repository queries use the restricted role.
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

            // 1. Owners can play their own memories regardless
            // of visibility or moderation status.
            Assert.NotNull(await repository.GetByIdForPlaybackAsync(
                privateMemory.Id,
                userAId));

            Assert.NotNull(await repository.GetByIdForPlaybackAsync(
                pendingOwnerMemory.Id,
                userAId));

            Assert.NotNull(await repository.GetByIdForPlaybackAsync(
                rejectedOwnerMemory.Id,
                userAId));

            // 2. Other users can play approved public memories.
            Assert.NotNull(await repository.GetByIdForPlaybackAsync(
                publicMemory.Id,
                userBId));

            // 3. Without an accepted friendship, friends-only
            // memories must remain inaccessible.
            Assert.Null(await repository.GetByIdForPlaybackAsync(
                friendsOnlyMemoryA.Id,
                userBId));

            Assert.Null(await repository.GetByIdForPlaybackAsync(
                friendsOnlyMemoryB.Id,
                userAId));

            // 4. Create an accepted friendship.
            adminContext.Friendships.Add(friendship);
            await adminContext.SaveChangesAsync();

            // 5. Both friendship orientations must work.
            // User B accesses User A's memory.
            Assert.NotNull(await repository.GetByIdForPlaybackAsync(
                friendsOnlyMemoryA.Id,
                userBId));

            // User A accesses User B's memory.
            Assert.NotNull(await repository.GetByIdForPlaybackAsync(
                friendsOnlyMemoryB.Id,
                userAId));

            // 6. Pending friendship must not grant access.
            friendship.Status = FriendshipStatus.Pending;
            await adminContext.SaveChangesAsync();

            Assert.Null(await repository.GetByIdForPlaybackAsync(
                friendsOnlyMemoryA.Id,
                userBId));

            // 7. Blocked friendship must not grant access.
            friendship.Status = FriendshipStatus.Blocked;
            await adminContext.SaveChangesAsync();

            Assert.Null(await repository.GetByIdForPlaybackAsync(
                friendsOnlyMemoryA.Id,
                userBId));

            // 8. Other users cannot play private memories.
            Assert.Null(await repository.GetByIdForPlaybackAsync(
                otherPrivateMemory.Id,
                userBId));

            // 9. Other users cannot play pending or rejected memories.
            Assert.Null(await repository.GetByIdForPlaybackAsync(
                pendingPublicMemory.Id,
                userBId));

            Assert.Null(await repository.GetByIdForPlaybackAsync(
                rejectedPublicMemory.Id,
                userBId));

            // 10. Deleted memories cannot be played,
            // including by their owners.
            Assert.Null(await repository.GetByIdForPlaybackAsync(
                deletedMemory.Id,
                userAId));

            Assert.Null(await repository.GetByIdForPlaybackAsync(
                deletedMemory.Id,
                userBId));

            // 11. Transaction-local identity must not leak.
            var visibleMemories =
                await restrictedContext.Memories
                    .AsNoTracking()
                    .Where(memory => memoryIds.Contains(memory.Id))
                    .ToListAsync();

            Assert.DoesNotContain(
                visibleMemories,
                memory => memory.Id == privateMemory.Id);

            Assert.DoesNotContain(
                visibleMemories,
                memory => memory.Id == pendingOwnerMemory.Id);

            Assert.DoesNotContain(
                visibleMemories,
                memory => memory.Id == rejectedOwnerMemory.Id);
        }
        finally
        {
            // Remove only records created by this test.
            adminContext.ChangeTracker.Clear();

            await adminContext.Friendships
                .Where(item => item.Id == friendship.Id)
                .ExecuteDeleteAsync();

            await adminContext.Memories
                .Where(memory => memoryIds.Contains(memory.Id))
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
        MemoryVisibility visibility,
        ModerationStatus moderationStatus,
        bool deleted = false)
    {
        var memoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        return new Memory
        {
            Id = memoryId,
            UserId = userId,
            Title = $"RLS Test {memoryId:N}",
            Description = "Temporary playback authorization test.",
            AudioPath = $"{userId}/{memoryId}.m4a",
            AudioDurationSeconds = 30,
            AudioContentType = "audio/mp4",
            AudioSizeBytes = 1024,
            Location = new Point(101.6869, 3.1390)
            {
                SRID = 4326
            },
            Visibility = visibility,
            NotificationRadiusMeters = 50,
            ModerationStatus = moderationStatus,
            CreatedAt = now,
            UpdatedAt = now,
            DeletedAt = deleted ? now : null
        };
    }

    private static Friendship CreateFriendship(
        Guid userAId,
        Guid userBId)
    {
        var userAIsFirst = userAId.CompareTo(userBId) < 0;

        var userId = userAIsFirst ? userAId : userBId;
        var friendId = userAIsFirst ? userBId : userAId;

        var now = DateTimeOffset.UtcNow;

        return new Friendship
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FriendId = friendId,
            RequestedBy = userAId,
            Status = FriendshipStatus.Accepted,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class MemoryAccessDatabaseFactAttribute : FactAttribute
{
    public MemoryAccessDatabaseFactAttribute()
    {
        var requiredVariables = new[]
        {
            "SONICMEMORYMAPS_RLS_TEST_CONNECTION_STRING",
            "SONICMEMORYMAPS_RLS_TEST_ADMIN_CONNECTION_STRING",
            "SONICMEMORYMAPS_RLS_TEST_USER_A_ID",
            "SONICMEMORYMAPS_RLS_TEST_USER_B_ID"
        };

        if (requiredVariables.Any(variable =>
            string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(variable))))
        {
            Skip = "Memory access database test skipped. Configure all required database test environment variables.";
        }
    }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class AdminDatabaseFactAttribute : FactAttribute
{
    public AdminDatabaseFactAttribute()
    {
        var requiredVariables = new[]
        {
            "SONICMEMORYMAPS_RLS_TEST_CONNECTION_STRING",
            "SONICMEMORYMAPS_RLS_TEST_ADMIN_CONNECTION_STRING",
            "SONICMEMORYMAPS_RLS_TEST_USER_A_ID",
            "SONICMEMORYMAPS_RLS_TEST_USER_B_ID"
        };

        if (requiredVariables.Any(variable =>
            string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(variable))))
        {
            Skip = "Admin database test skipped. Configure all required RLS test environment variables.";
        }
    }
}
