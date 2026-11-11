
namespace SonicMemoryMaps.Domain.Entities;

public sealed class Profile
{
    public Guid Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public string? AvatarUrl { get; set; }

    public string? Bio { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<Memory> Memories { get; set; } = [];
}
