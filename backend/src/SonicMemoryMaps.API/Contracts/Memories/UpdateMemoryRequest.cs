using SonicMemoryMaps.Domain.Enums;

namespace SonicMemoryMaps.API.Contracts.Memories;

public sealed record UpdateMemoryRequest(
    string Title,
    string? Description,
    MemoryVisibility Visibility,
    int NotificationRadiusMeters
);