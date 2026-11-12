
namespace SonicMemoryMaps.Application.Profiles;

public sealed record ProfileDto(
    Guid Id,
    string Username,
    string? DisplayName,
    string? AvatarUrl,
    string? Bio,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
