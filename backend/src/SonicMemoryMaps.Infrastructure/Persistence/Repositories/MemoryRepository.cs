
using Microsoft.EntityFrameworkCore;
using SonicMemoryMaps.Application.Abstractions;
using SonicMemoryMaps.Domain.Entities;

namespace SonicMemoryMaps.Infrastructure.Persistence.Repositories;

public sealed class MemoryRepository(
    SonicMemoryMapsDbContext dbContext) : IMemoryRepository
{
    public async Task AddAsync(
        Memory memory,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (memory.UserId != userId)
        {
            throw new InvalidOperationException(
                "A user cannot create a memory on behalf of another user.");
        }

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        await SetUserContextAsync(userId, cancellationToken);

        dbContext.Memories.Add(memory);

        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<Memory?> GetByIdForOwnerAsync(
        Guid memoryId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        await SetUserContextAsync(userId, cancellationToken);

        var memory = await dbContext.Memories
            .AsNoTracking()
            .FirstOrDefaultAsync(
                memory =>
                    memory.Id == memoryId &&
                    memory.UserId == userId &&
                    memory.DeletedAt == null,
                cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return memory;
    }

    public async Task<Memory?> GetTrackedByIdForOwnerAsync(
        Guid memoryId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        await SetUserContextAsync(userId, cancellationToken);

        var memory = await dbContext.Memories
            .FirstOrDefaultAsync(
                memory =>
                    memory.Id == memoryId &&
                    memory.UserId == userId &&
                    memory.DeletedAt == null,
                cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return memory;
    }

    public async Task<IReadOnlyList<Memory>> GetByOwnerAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        await SetUserContextAsync(userId, cancellationToken);

        var memories = await dbContext.Memories
            .AsNoTracking()
            .Where(memory =>
                memory.UserId == userId &&
                memory.DeletedAt == null)
            .OrderByDescending(memory => memory.CreatedAt)
            .ToListAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return memories;
    }

    public async Task SaveChangesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var modifiedMemories = dbContext.ChangeTracker
            .Entries<Memory>()
            .Where(entry =>
                entry.State == EntityState.Modified ||
                entry.State == EntityState.Added);

        if (modifiedMemories.Any(entry => entry.Entity.UserId != userId))
        {
            throw new InvalidOperationException(
                "A user cannot modify a memory owned by another user.");
        }

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        await SetUserContextAsync(userId, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task SetUserContextAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.user_id', {userId.ToString()}, true)",
            cancellationToken);
    }
}
