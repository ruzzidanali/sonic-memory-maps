using SonicMemoryMaps.Domain.Enums;

namespace SonicMemoryMaps.Application.Memories;

public sealed record UpdateMemoryCommand(
    Guid MemoryId,
    string Title,
    string? Description,
    MemoryVisibility Visibility,
    int NotificationRadiusMeters
);