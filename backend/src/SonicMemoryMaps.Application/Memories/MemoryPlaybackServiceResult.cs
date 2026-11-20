namespace SonicMemoryMaps.Application.Memories;

public enum MemoryPlaybackServiceStatus
{
    Success,
    Unauthorized,
    NotFound
}

public sealed record MemoryPlaybackServiceResult(
    MemoryPlaybackServiceStatus Status,
    MemoryPlaybackDto? Data = null,
    string? Error = null
);