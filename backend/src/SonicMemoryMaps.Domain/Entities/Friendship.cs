
using SonicMemoryMaps.Domain.Enums;

namespace SonicMemoryMaps.Domain.Entities;

public sealed class Friendship
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid FriendId { get; set; }

    public Guid RequestedBy { get; set; }

    public FriendshipStatus Status { get; set; }
        = FriendshipStatus.Pending;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
