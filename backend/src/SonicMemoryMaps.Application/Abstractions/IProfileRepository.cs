
using SonicMemoryMaps.Domain.Entities;

namespace SonicMemoryMaps.Application.Abstractions;

public interface IProfileRepository
{
    Task<Profile?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Profile?> GetTrackedByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> SaveChangesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
