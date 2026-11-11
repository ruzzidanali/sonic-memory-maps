
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SonicMemoryMaps.Domain.Entities;

namespace SonicMemoryMaps.Infrastructure.Persistence.Configurations;

public sealed class MemoryReportConfiguration : IEntityTypeConfiguration<MemoryReport>
{
    public void Configure(EntityTypeBuilder<MemoryReport> entity)
    {
        entity.ToTable("memory_reports", table =>
        {
            table.HasCheckConstraint(
                "ck_memory_reports_reason",
                "reason IN ('inappropriate', 'spam', 'harassment', 'copyright', 'other')");

            table.HasCheckConstraint(
                "ck_memory_reports_status",
                "status IN ('pending', 'reviewed', 'dismissed')");
        });

        entity.HasKey(x => x.Id)
            .HasName("memory_reports_pkey");

        entity.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        entity.Property(x => x.MemoryId)
            .HasColumnName("memory_id")
            .IsRequired();

        entity.Property(x => x.ReporterId)
            .HasColumnName("reporter_id")
            .IsRequired();

        entity.Property(x => x.Reason)
            .HasColumnName("reason")
            .HasMaxLength(50)
            .IsRequired();

        entity.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        entity.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasDefaultValue("pending")
            .IsRequired();

        entity.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();

        entity.Property(x => x.ReviewedAt)
            .HasColumnName("reviewed_at");

        entity.HasOne<Memory>()
            .WithMany()
            .HasForeignKey(x => x.MemoryId)
            .HasConstraintName("memory_reports_memory_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne<Profile>()
            .WithMany()
            .HasForeignKey(x => x.ReporterId)
            .HasConstraintName("memory_reports_reporter_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(x => new
            {
                x.MemoryId,
                x.ReporterId
            })
            .IsUnique()
            .HasDatabaseName("ux_memory_reports_reporter_memory");

        entity.HasIndex(x => new
            {
                x.Status,
                x.CreatedAt
            })
            .HasDatabaseName("ix_memory_reports_status");
    }
}
