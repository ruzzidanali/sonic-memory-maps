
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SonicMemoryMaps.Domain.Entities;
using SonicMemoryMaps.Domain.Enums;

namespace SonicMemoryMaps.Infrastructure.Persistence.Configurations;

public sealed class MemoryConfiguration : IEntityTypeConfiguration<Memory>
{
    public void Configure(EntityTypeBuilder<Memory> entity)
    {
        entity.ToTable("memories", table =>
        {
            table.HasCheckConstraint(
                "ck_memories_title",
                "char_length(TRIM(BOTH FROM title)) >= 1 AND " +
                "char_length(TRIM(BOTH FROM title)) <= 100");

            table.HasCheckConstraint(
                "ck_memories_audio_duration",
                "audio_duration_seconds >= 1 AND audio_duration_seconds <= 600");

            table.HasCheckConstraint(
                "ck_memories_audio_size",
                "audio_size_bytes >= 1 AND audio_size_bytes <= 52428800");

            table.HasCheckConstraint(
                "ck_memories_notification_radius",
                "notification_radius_meters >= 10 AND notification_radius_meters <= 250");

            table.HasCheckConstraint(
                "ck_memories_visibility",
                "visibility IN ('public', 'friends_only', 'private')");

            table.HasCheckConstraint(
                "ck_memories_moderation_status",
                "moderation_status IN ('pending', 'approved', 'rejected')");
        });

        entity.HasKey(x => x.Id)
            .HasName("memories_pkey");

        entity.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        entity.Property(x => x.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        entity.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(100)
            .IsRequired();

        entity.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        entity.Property(x => x.AudioPath)
            .HasColumnName("audio_path")
            .HasColumnType("text")
            .IsRequired();

        entity.Property(x => x.AudioDurationSeconds)
            .HasColumnName("audio_duration_seconds");

        entity.Property(x => x.AudioContentType)
            .HasColumnName("audio_content_type")
            .HasMaxLength(100)
            .IsRequired();

        entity.Property(x => x.AudioSizeBytes)
            .HasColumnName("audio_size_bytes");

        entity.Property(x => x.Location)
            .HasColumnName("location")
            .HasColumnType("geography(Point,4326)")
            .IsRequired();

        entity.Property(x => x.Visibility)
            .HasColumnName("visibility")
            .HasConversion(
                value => ToVisibilityDatabase(value),
                value => FromVisibilityDatabase(value))
            .HasMaxLength(20)
            .HasDefaultValueSql("'public'");

        entity.Property(x => x.NotificationRadiusMeters)
            .HasColumnName("notification_radius_meters")
            .HasDefaultValue(50);

        entity.Property(x => x.ModerationStatus)
            .HasColumnName("moderation_status")
            .HasConversion(
                value => ToModerationDatabase(value),
                value => FromModerationDatabase(value))
            .HasMaxLength(20)
            .HasDefaultValueSql("'pending'");

        entity.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();

        entity.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();

        entity.Property(x => x.DeletedAt)
            .HasColumnName("deleted_at");

        entity.HasOne(x => x.User)
            .WithMany(x => x.Memories)
            .HasForeignKey(x => x.UserId)
            .HasConstraintName("memories_user_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(x => x.UserId)
            .HasDatabaseName("ix_memories_user_id");

        entity.HasIndex(x => x.Location)
            .HasMethod("gist")
            .HasDatabaseName("ix_memories_location");

        entity.HasIndex(x => x.CreatedAt)
            .IsDescending()
            .HasDatabaseName("ix_memories_created_at");

        entity.HasIndex(x => new
        {
            x.Visibility,
            x.ModerationStatus
        })
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("ix_memories_visibility");
    }


    private static string ToVisibilityDatabase(MemoryVisibility value)
    {
        return value switch
        {
            MemoryVisibility.Public => "public",
            MemoryVisibility.FriendsOnly => "friends_only",
            MemoryVisibility.Private => "private",
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };
    }

    private static MemoryVisibility FromVisibilityDatabase(string value)
    {
        return value switch
        {
            "public" => MemoryVisibility.Public,
            "friends_only" => MemoryVisibility.FriendsOnly,
            "private" => MemoryVisibility.Private,
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };
    }

    private static string ToModerationDatabase(ModerationStatus value)
    {
        return value switch
        {
            ModerationStatus.Pending => "pending",
            ModerationStatus.Approved => "approved",
            ModerationStatus.Rejected => "rejected",
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };
    }

    private static ModerationStatus FromModerationDatabase(string value)
    {
        return value switch
        {
            "pending" => ModerationStatus.Pending,
            "approved" => ModerationStatus.Approved,
            "rejected" => ModerationStatus.Rejected,
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };
    }

}
