namespace SonicMemoryMaps.Application.Memories;

public sealed record MemoryPlaybackDto(
    Guid MemoryId,
    string PlaybackUrl,
    DateTimeOffset ExpiresAt
);