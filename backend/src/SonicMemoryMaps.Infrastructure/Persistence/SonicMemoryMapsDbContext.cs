
using Microsoft.EntityFrameworkCore;
using SonicMemoryMaps.Domain.Entities;

namespace SonicMemoryMaps.Infrastructure.Persistence;

public sealed class SonicMemoryMapsDbContext(
    DbContextOptions<SonicMemoryMapsDbContext> options)
    : DbContext(options)
{
    public DbSet<Profile> Profiles => Set<Profile>();

    public DbSet<Memory> Memories => Set<Memory>();

    public DbSet<MemoryLike> MemoryLikes => Set<MemoryLike>();

    public DbSet<MemoryReport> MemoryReports => Set<MemoryReport>();

    public DbSet<Friendship> Friendships => Set<Friendship>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("public");

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(SonicMemoryMapsDbContext).Assembly);
    }
}
