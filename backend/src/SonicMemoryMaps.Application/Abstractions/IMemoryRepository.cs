using SonicMemoryMaps.Domain.Entities;

namespace SonicMemoryMaps.Application.Abstractions;

public interface IMemoryRepository
{
    Task AddAsync(
        Memory memory,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Memory?> GetByIdForOwnerAsync(
        Guid memoryId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Memory?> GetTrackedByIdForOwnerAsync(
        Guid memoryId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Memory>> GetByOwnerAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}