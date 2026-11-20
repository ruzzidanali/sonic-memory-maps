using SonicMemoryMaps.Application.Abstractions;

namespace SonicMemoryMaps.Application.Memories;

public sealed class MemoryPlaybackService(
    ICurrentUser currentUser,
    IMemoryAccessRepository memoryAccessRepository,
    IAudioStorageService audioStorageService) : IMemoryPlaybackService
{
    public async Task<MemoryPlaybackServiceResult> CreatePlaybackAuthorizationAsync(
        Guid memoryId,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated ||
            currentUser.UserId is not Guid userId)
        {
            return new MemoryPlaybackServiceResult(
                MemoryPlaybackServiceStatus.Unauthorized,
                Error: "Authentication is required.");
        }

        if (memoryId == Guid.Empty)
        {
            return new MemoryPlaybackServiceResult(
                MemoryPlaybackServiceStatus.NotFound,
                Error: "Memory not found.");
        }

        var memory = await memoryAccessRepository.GetByIdForPlaybackAsync(
            memoryId,
            userId,
            cancellationToken);

        if (memory is null)
        {
            return new MemoryPlaybackServiceResult(
                MemoryPlaybackServiceStatus.NotFound,
                Error: "Memory not found or access is denied.");
        }

        var authorization =
            await audioStorageService.CreateDownloadAuthorizationAsync(
                memory.UserId,
                memory.Id,
                memory.AudioContentType,
                cancellationToken);

        var data = new MemoryPlaybackDto(
            memory.Id,
            authorization.DownloadUrl,
            authorization.ExpiresAt);

        return new MemoryPlaybackServiceResult(
            MemoryPlaybackServiceStatus.Success,
            Data: data);
    }
}