namespace SonicMemoryMaps.API.DTOs.Profiles;

public sealed record ProfileResponse(
    Guid Id,
    string Username,
    string? DisplayName,
    string? AvatarUrl,
    string? Bio,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);