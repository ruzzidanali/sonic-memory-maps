using SonicMemoryMaps.Domain.Enums;

namespace SonicMemoryMaps.Application.Memories;

public sealed record MemoryDto(
    Guid Id,
    Guid UserId,
    string Title,
    string? Description,
    int AudioDurationSeconds,
    string AudioContentType,
    long AudioSizeBytes,
    double Latitude,
    double Longitude,
    MemoryVisibility Visibility,
    int NotificationRadiusMeters,
    ModerationStatus ModerationStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);