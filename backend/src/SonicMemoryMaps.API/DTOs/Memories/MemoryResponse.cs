using SonicMemoryMaps.Domain.Enums;

namespace SonicMemoryMaps.API.DTOs.Memories;

public sealed record MemoryResponse(
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