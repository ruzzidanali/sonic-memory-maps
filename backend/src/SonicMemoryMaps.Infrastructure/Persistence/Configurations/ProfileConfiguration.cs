
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SonicMemoryMaps.Domain.Entities;

namespace SonicMemoryMaps.Infrastructure.Persistence.Configurations;

public sealed class ProfileConfiguration : IEntityTypeConfiguration<Profile>
{
    public void Configure(EntityTypeBuilder<Profile> entity)
    {
        entity.ToTable("profiles");

        entity.HasKey(x => x.Id)
            .HasName("profiles_pkey");

        entity.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        entity.Property(x => x.Username)
            .HasColumnName("username")
            .HasMaxLength(30)
            .IsRequired();

        entity.Property(x => x.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(100);

        entity.Property(x => x.AvatarUrl)
            .HasColumnName("avatar_url")
            .HasColumnType("text");

        entity.Property(x => x.Bio)
            .HasColumnName("bio")
            .HasMaxLength(300);

        entity.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();

        entity.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();

    }
}
