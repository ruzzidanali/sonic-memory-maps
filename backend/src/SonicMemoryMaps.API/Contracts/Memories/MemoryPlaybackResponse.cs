namespace SonicMemoryMaps.API.Contracts.Memories;

public sealed record MemoryPlaybackResponse(
    Guid MemoryId,
    string PlaybackUrl,
    DateTimeOffset ExpiresAt
);