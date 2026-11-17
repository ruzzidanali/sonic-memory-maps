using SonicMemoryMaps.Domain.Enums;

namespace SonicMemoryMaps.API.Contracts.Memories;

public sealed record CreateMemoryRequest(
    Guid MemoryId,
    string Title,
    string? Description,
    string RequestedAudioContentType,
    int AudioDurationSeconds,
    double Latitude,
    double Longitude,
    MemoryVisibility Visibility,
    int NotificationRadiusMeters = 50
);