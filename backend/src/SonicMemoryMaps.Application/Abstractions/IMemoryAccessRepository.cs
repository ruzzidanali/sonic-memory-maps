
using SonicMemoryMaps.Domain.Entities;

namespace SonicMemoryMaps.Application.Abstractions;

public interface IMemoryAccessRepository
{
    Task<Memory?> GetByIdForPlaybackAsync(
        Guid memoryId,
        Guid requestingUserId,
        CancellationToken cancellationToken = default);
}
