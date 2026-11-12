
namespace SonicMemoryMaps.Application.Profiles;

public sealed record UpdateProfileCommand(
    string Username,
    string? DisplayName,
    string? Bio
);
