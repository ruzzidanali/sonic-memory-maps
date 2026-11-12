using System.ComponentModel.DataAnnotations;

namespace SonicMemoryMaps.API.DTOs.Profiles;

public sealed class UpdateProfileRequest
{
    [Required]
    [StringLength(30, MinimumLength = 3)]
    public string Username { get; init; } = string.Empty;

    [StringLength(100)]
    public string? DisplayName { get; init; }

    [StringLength(300)]
    public string? Bio { get; init; }
}