
using SonicMemoryMaps.Domain.Enums;

namespace SonicMemoryMaps.Application.Memories;

public sealed record CreateMemoryCommand(
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
