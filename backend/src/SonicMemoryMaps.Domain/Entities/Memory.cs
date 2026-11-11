
using NetTopologySuite.Geometries;
using SonicMemoryMaps.Domain.Enums;

namespace SonicMemoryMaps.Domain.Entities;

public sealed class Memory
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string AudioPath { get; set; } = string.Empty;

    public int AudioDurationSeconds { get; set; }

    public string AudioContentType { get; set; } = string.Empty;

    public long AudioSizeBytes { get; set; }

    public Point Location { get; set; } = default!;

    public MemoryVisibility Visibility { get; set; }

    public int NotificationRadiusMeters { get; set; } = 50;

    public ModerationStatus ModerationStatus { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public Profile User { get; set; } = default!;

    public ICollection<MemoryLike> Likes { get; set; } = [];

}
