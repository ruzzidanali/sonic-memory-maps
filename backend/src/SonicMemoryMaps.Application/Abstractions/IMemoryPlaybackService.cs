using SonicMemoryMaps.Application.Memories;

namespace SonicMemoryMaps.Application.Abstractions;

public interface IMemoryPlaybackService
{
    Task<MemoryPlaybackServiceResult> CreatePlaybackAuthorizationAsync(
        Guid memoryId,
        CancellationToken cancellationToken = default);
}