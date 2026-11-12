
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SonicMemoryMaps.Application.Abstractions;
using SonicMemoryMaps.Domain.Entities;
using SonicMemoryMaps.Infrastructure.Persistence;

namespace SonicMemoryMaps.Infrastructure.Persistence.Repositories;

public sealed class ProfileRepository(
    SonicMemoryMapsDbContext dbContext) : IProfileRepository
{
    private const string UsernameUniqueIndex =
        "ux_profiles_username_lower";

    public async Task<Profile?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        await SetUserContextAsync(userId, cancellationToken);

        var profile = await dbContext.Profiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                profile => profile.Id == userId,
                cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return profile;
    }

    public async Task<Profile?> GetTrackedByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        await SetUserContextAsync(userId, cancellationToken);

        var profile = await dbContext.Profiles
            .FirstOrDefaultAsync(
                profile => profile.Id == userId,
                cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return profile;
    }

    public async Task<bool> SaveChangesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        await SetUserContextAsync(userId, cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return true;
        }
        catch (DbUpdateException exception)
            when (IsUsernameUniqueViolation(exception))
        {
            await transaction.RollbackAsync(CancellationToken.None);

            return false;
        }
    }

    private async Task SetUserContextAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.user_id', {userId.ToString()}, true)",
            cancellationToken);
    }

    private static bool IsUsernameUniqueViolation(
        DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: UsernameUniqueIndex
        };
    }
}
