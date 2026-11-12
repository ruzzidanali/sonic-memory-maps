
using Microsoft.EntityFrameworkCore;
using SonicMemoryMaps.Domain.Entities;
using SonicMemoryMaps.Infrastructure.Persistence;
using SonicMemoryMaps.Infrastructure.Persistence.Repositories;

namespace SonicMemoryMaps.IntegrationTests.Repositories;

[Trait("Category", "Database")]
public sealed class ProfileRepositoryDatabaseTests
{
    private const string ConnectionStringVariable =
        "SONICMEMORYMAPS_RLS_TEST_CONNECTION_STRING";

    private const string UserAVariable =
        "SONICMEMORYMAPS_RLS_TEST_USER_A_ID";

    private const string UserBVariable =
        "SONICMEMORYMAPS_RLS_TEST_USER_B_ID";

    [DatabaseFact]
    public async Task ProfileRepository_EnforcesRlsAndUpdatesOwnProfile()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(ConnectionStringVariable);

        var userAValue =
            Environment.GetEnvironmentVariable(UserAVariable);

        var userBValue =
            Environment.GetEnvironmentVariable(UserBVariable);

        if (string.IsNullOrWhiteSpace(connectionString) ||
            string.IsNullOrWhiteSpace(userAValue) ||
            string.IsNullOrWhiteSpace(userBValue))
        {
            throw new InvalidOperationException(
                "Database test configuration is missing.");
        }

        if (!Guid.TryParse(userAValue, out var userAId) ||
            !Guid.TryParse(userBValue, out var userBId))
        {
            throw new InvalidOperationException(
                "The RLS test user IDs must be valid UUIDs.");
        }

        if (userAId == userBId)
        {
            throw new InvalidOperationException(
                "User A and User B must be different accounts.");
        }

        var options = new DbContextOptionsBuilder<SonicMemoryMapsDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.UseNetTopologySuite())
            .Options;

        Profile? originalProfile = null;
        var restorationRequired = false;

        try
        {
            await using var dbContext =
                new SonicMemoryMapsDbContext(options);

            var repository = new ProfileRepository(dbContext);

            await dbContext.Database.OpenConnectionAsync();

            try
            {
                await using var command =
                    dbContext.Database.GetDbConnection().CreateCommand();

                command.CommandText = "SELECT current_user";

                var currentRole =
                    (string?)await command.ExecuteScalarAsync();

                Assert.Equal("sonic_api", currentRole);
            }
            finally
            {
                await dbContext.Database.CloseConnectionAsync();
            }

            // 1. Verify that User A can retrieve their own profile.
            originalProfile = await repository.GetByIdAsync(userAId);

            Assert.NotNull(originalProfile);
            Assert.Equal(userAId, originalProfile.Id);

            await using (var transaction =
    await dbContext.Database.BeginTransactionAsync())
            {
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT set_config('app.user_id', {userAId.ToString()}, true)");

                var userBProfile = await dbContext.Profiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        profile => profile.Id == userBId);

                Assert.Null(userBProfile);

                await transaction.CommitAsync();
            }

            // 2. Verify that transaction-local identity does not leak.
            var profilesOutsideTransaction =
                await dbContext.Profiles
                    .AsNoTracking()
                    .ToListAsync();

            Assert.Empty(profilesOutsideTransaction);

            // 3. Verify tracked profile retrieval.
            var trackedProfile =
                await repository.GetTrackedByIdAsync(userAId);

            Assert.NotNull(trackedProfile);
            Assert.Equal(userAId, trackedProfile.Id);

            // 4. Temporarily update editable profile fields.
            var temporaryDisplayName =
                $"RLS Test {Guid.NewGuid():N}";

            var temporaryBio =
                $"Temporary RLS integration test {Guid.NewGuid():N}";

            trackedProfile.DisplayName = temporaryDisplayName;
            trackedProfile.Bio = temporaryBio;
            trackedProfile.UpdatedAt = DateTimeOffset.UtcNow;

            restorationRequired = true;

            var saved = await repository.SaveChangesAsync(userAId);

            Assert.True(saved);

            // 5. Verify the update using a fresh DbContext.
            await using var verificationContext =
                new SonicMemoryMapsDbContext(options);

            var verificationRepository =
                new ProfileRepository(verificationContext);

            var updatedProfile =
                await verificationRepository.GetByIdAsync(userAId);

            Assert.NotNull(updatedProfile);
            Assert.Equal(temporaryDisplayName, updatedProfile.DisplayName);
            Assert.Equal(temporaryBio, updatedProfile.Bio);

            // 6. Verify identity is reset after the transaction.
            var visibleProfiles =
                await verificationContext.Profiles
                    .AsNoTracking()
                    .ToListAsync();

            Assert.Empty(visibleProfiles);
        }
        finally
        {
            // Restore the original profile values even if an assertion fails.
            if (restorationRequired && originalProfile is not null)
            {
                await using var restorationContext =
                    new SonicMemoryMapsDbContext(options);

                var restorationRepository =
                    new ProfileRepository(restorationContext);

                var profileToRestore =
                    await restorationRepository.GetTrackedByIdAsync(userAId);

                if (profileToRestore is null)
                {
                    throw new InvalidOperationException(
                        "Could not retrieve the profile for restoration.");
                }

                profileToRestore.DisplayName =
                    originalProfile.DisplayName;

                profileToRestore.Bio =
                    originalProfile.Bio;

                profileToRestore.UpdatedAt =
                    originalProfile.UpdatedAt;

                var restored =
                    await restorationRepository.SaveChangesAsync(userAId);

                if (!restored)
                {
                    throw new InvalidOperationException(
                        "Could not restore the original profile values.");
                }
            }
        }
    }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class DatabaseFactAttribute : FactAttribute
{
    public DatabaseFactAttribute()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "SONICMEMORYMAPS_RLS_TEST_CONNECTION_STRING");

        var userA =
            Environment.GetEnvironmentVariable(
                "SONICMEMORYMAPS_RLS_TEST_USER_A_ID");

        var userB =
            Environment.GetEnvironmentVariable(
                "SONICMEMORYMAPS_RLS_TEST_USER_B_ID");

        if (string.IsNullOrWhiteSpace(connectionString) ||
            string.IsNullOrWhiteSpace(userA) ||
            string.IsNullOrWhiteSpace(userB))
        {
            Skip = "Database test skipped. Configure the RLS test environment variables.";
        }
    }
}
