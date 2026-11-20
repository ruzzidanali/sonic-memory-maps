
using Microsoft.EntityFrameworkCore;
using SonicMemoryMaps.Application.Abstractions;
using SonicMemoryMaps.Domain.Entities;
using SonicMemoryMaps.Domain.Enums;

namespace SonicMemoryMaps.Infrastructure.Persistence.Repositories;

public sealed class MemoryAccessRepository(
    SonicMemoryMapsDbContext dbContext) : IMemoryAccessRepository
{
    public async Task<Memory?> GetByIdForPlaybackAsync(
        Guid memoryId,
        Guid requestingUserId,
        CancellationToken cancellationToken = default)
    {
        if (memoryId == Guid.Empty)
            throw new ArgumentException(
                "Memory ID is required.", nameof(memoryId));

        if (requestingUserId == Guid.Empty)
            throw new ArgumentException(
                "Requesting user ID is required.", nameof(requestingUserId));

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.user_id', {requestingUserId.ToString()}, true)",
            cancellationToken);

        var memory = await dbContext.Memories
            .AsNoTracking()
            .Where(memory =>
                memory.Id == memoryId &&
                memory.DeletedAt == null)
            .Where(memory =>
                // Owners can play their own memories regardless
                // of moderation status.
                memory.UserId == requestingUserId ||

                // Other users can play approved public memories.
                (
                    memory.ModerationStatus == ModerationStatus.Approved &&
                    (
                        memory.Visibility == MemoryVisibility.Public ||

                        // Friends can play approved friends-only memories.
                        (
                            memory.Visibility == MemoryVisibility.FriendsOnly &&
                            dbContext.Friendships.Any(friendship =>
                                friendship.Status == FriendshipStatus.Accepted &&
                                (
                                    (
                                        friendship.UserId == memory.UserId &&
                                        friendship.FriendId == requestingUserId
                                    ) ||
                                    (
                                        friendship.UserId == requestingUserId &&
                                        friendship.FriendId == memory.UserId
                                    )
                                )
                            )
                        )
                    )
                )
            )
            .FirstOrDefaultAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return memory;
    }
}
