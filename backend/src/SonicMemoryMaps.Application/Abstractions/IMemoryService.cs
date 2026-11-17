
using SonicMemoryMaps.Application.Memories;

namespace SonicMemoryMaps.Application.Abstractions;

public interface IMemoryService
{
    Task<MemoryServiceResult<MemoryDto>> CreateMemoryAsync(
        CreateMemoryCommand command,
        CancellationToken cancellationToken = default);

    Task<MemoryServiceResult<IReadOnlyList<MemoryDto>>> GetMyMemoriesAsync(
        CancellationToken cancellationToken = default);

    Task<MemoryServiceResult<MemoryDto>> GetMemoryByIdAsync(
        Guid memoryId,
        CancellationToken cancellationToken = default);

    Task<MemoryServiceResult<MemoryDto>> UpdateMemoryAsync(
        UpdateMemoryCommand command,
        CancellationToken cancellationToken = default);

    Task<MemoryServiceResult<bool>> DeleteMemoryAsync(
        Guid memoryId,
        CancellationToken cancellationToken = default);
}
