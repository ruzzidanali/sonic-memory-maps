using SonicMemoryMaps.Domain.Enums;

namespace SonicMemoryMaps.Domain.Entities;

public sealed class MemoryReport
{
    public Guid Id { get; set; }

    public Guid MemoryId { get; set; }

    public Guid ReporterId { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string? Description { get; set; }

    public ReportStatus Status { get; set; } = ReportStatus.Pending;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }
}