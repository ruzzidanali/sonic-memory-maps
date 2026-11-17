
using System.Collections.Concurrent;
using SonicMemoryMaps.Application.Abstractions;
using SonicMemoryMaps.Domain.Entities;

namespace SonicMemoryMaps.IntegrationTests.Repositories;

public sealed class InMemoryMemoryRepository : IMemoryRepository
{
    private readonly ConcurrentDictionary<Guid, Memory> _memories = new();

    public int AddCallCount { get; private set; }

    public void SeedMemory(Memory memory)
    {
        _memories[memory.Id] = memory;
    }

    public Task AddAsync(
        Memory memory,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (memory.UserId != userId)
        {
            throw new InvalidOperationException(
                "A user cannot create a memory on behalf of another user.");
        }

        memory.Id = memory.Id == Guid.Empty
            ? Guid.NewGuid()
            : memory.Id;

        var now = DateTimeOffset.UtcNow;

        if (memory.CreatedAt == default)
        {
            memory.CreatedAt = now;
        }

        if (memory.UpdatedAt == default)
        {
            memory.UpdatedAt = now;
        }

        _memories[memory.Id] = memory;
        AddCallCount++;

        return Task.CompletedTask;
    }

    public Task<Memory?> GetByIdForOwnerAsync(
        Guid memoryId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _memories.TryGetValue(memoryId, out var memory);

        var result = memory is not null &&
                     memory.UserId == userId &&
                     memory.DeletedAt is null
            ? Clone(memory)
            : null;

        return Task.FromResult(result);
    }

    public Task<Memory?> GetTrackedByIdForOwnerAsync(
        Guid memoryId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _memories.TryGetValue(memoryId, out var memory);

        var result = memory is not null &&
                     memory.UserId == userId &&
                     memory.DeletedAt is null
            ? memory
            : null;

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<Memory>> GetByOwnerAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<Memory> result = _memories.Values
            .Where(memory =>
                memory.UserId == userId &&
                memory.DeletedAt is null)
            .OrderByDescending(memory => memory.CreatedAt)
            .Select(Clone)
            .ToList();

        return Task.FromResult(result);
    }

    public Task SaveChangesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.CompletedTask;
    }

    private static Memory Clone(Memory memory)
    {
        return new Memory
        {
            Id = memory.Id,
            UserId = memory.UserId,
            Title = memory.Title,
            Description = memory.Description,
            AudioPath = memory.AudioPath,
            AudioDurationSeconds = memory.AudioDurationSeconds,
            AudioContentType = memory.AudioContentType,
            AudioSizeBytes = memory.AudioSizeBytes,
            Location = (NetTopologySuite.Geometries.Point)memory.Location.Copy(),
            Visibility = memory.Visibility,
            NotificationRadiusMeters = memory.NotificationRadiusMeters,
            ModerationStatus = memory.ModerationStatus,
            CreatedAt = memory.CreatedAt,
            UpdatedAt = memory.UpdatedAt,
            DeletedAt = memory.DeletedAt
        };
    }
}
