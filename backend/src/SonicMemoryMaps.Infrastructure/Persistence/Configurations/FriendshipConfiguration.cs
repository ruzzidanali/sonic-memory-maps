
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SonicMemoryMaps.Domain.Entities;
using SonicMemoryMaps.Domain.Enums;

namespace SonicMemoryMaps.Infrastructure.Persistence.Configurations;

public sealed class FriendshipConfiguration : IEntityTypeConfiguration<Friendship>
{
    public void Configure(EntityTypeBuilder<Friendship> entity)
    {
        entity.ToTable("friendships", table =>
        {
            table.HasCheckConstraint(
                "ck_friendships_distinct_users",
                "user_id < friend_id");

            table.HasCheckConstraint(
                "ck_friendships_requester",
                "requested_by = user_id OR requested_by = friend_id");

            table.HasCheckConstraint(
                "ck_friendships_status",
                "status IN ('pending', 'accepted', 'blocked')");
        });

        entity.HasKey(x => x.Id)
            .HasName("friendships_pkey");

        entity.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        entity.Property(x => x.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        entity.Property(x => x.FriendId)
            .HasColumnName("friend_id")
            .IsRequired();

        entity.Property(x => x.RequestedBy)
            .HasColumnName("requested_by")
            .IsRequired();

        entity.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion(
                value => ToDatabase(value),
                value => FromDatabase(value))
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

        entity.HasOne<Profile>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .HasConstraintName("friendships_user_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne<Profile>()
            .WithMany()
            .HasForeignKey(x => x.FriendId)
            .HasConstraintName("friendships_friend_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne<Profile>()
            .WithMany()
            .HasForeignKey(x => x.RequestedBy)
            .HasConstraintName("friendships_requested_by_fkey")
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(x => new
            {
                x.UserId,
                x.FriendId
            })
            .IsUnique()
            .HasDatabaseName("ux_friendships_pair");

        entity.HasIndex(x => new
            {
                x.FriendId,
                x.Status
            })
            .HasDatabaseName("ix_friendships_friend_id");
    }

    private static string ToDatabase(FriendshipStatus value)
    {
        return value switch
        {
            FriendshipStatus.Pending => "pending",
            FriendshipStatus.Accepted => "accepted",
            FriendshipStatus.Blocked => "blocked",
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };
    }

    private static FriendshipStatus FromDatabase(string value)
    {
        return value switch
        {
            "pending" => FriendshipStatus.Pending,
            "accepted" => FriendshipStatus.Accepted,
            "blocked" => FriendshipStatus.Blocked,
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };
    }
}
