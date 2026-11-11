
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SonicMemoryMaps.Domain.Entities;

namespace SonicMemoryMaps.Infrastructure.Persistence.Configurations;

public sealed class MemoryLikeConfiguration : IEntityTypeConfiguration<MemoryLike>
{
    public void Configure(EntityTypeBuilder<MemoryLike> entity)
    {
        entity.ToTable("memory_likes");

        entity.HasKey(x => x.Id)
            .HasName("memory_likes_pkey");

        entity.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        entity.Property(x => x.MemoryId)
            .HasColumnName("memory_id")
            .IsRequired();

        entity.Property(x => x.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        entity.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();

        entity.HasOne<Memory>()
            .WithMany()
            .HasForeignKey(x => x.MemoryId)
            .HasConstraintName("memory_likes_memory_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne<Profile>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .HasConstraintName("memory_likes_user_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(x => new
            {
                x.MemoryId,
                x.UserId
            })
            .IsUnique()
            .HasDatabaseName("ux_memory_likes_user_memory");

        entity.HasIndex(x => x.MemoryId)
            .HasDatabaseName("ix_memory_likes_memory_id");
    }
}
