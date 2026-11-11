namespace SonicMemoryMaps.Domain.Entities;

public sealed class MemoryLike
{
    public Guid Id { get; set; }
    public Guid MemoryId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}