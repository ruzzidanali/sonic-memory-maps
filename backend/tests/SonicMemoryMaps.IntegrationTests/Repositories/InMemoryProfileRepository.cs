
using System.Collections.Concurrent;
using SonicMemoryMaps.Application.Abstractions;
using SonicMemoryMaps.Domain.Entities;

namespace SonicMemoryMaps.IntegrationTests.Repositories;

public sealed class InMemoryProfileRepository : IProfileRepository
{
    private readonly ConcurrentDictionary<Guid, Profile> _profiles = new();

    public void SeedProfile(Profile profile)
    {
        _profiles[profile.Id] = profile;
    }

    public Task<Profile?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _profiles.TryGetValue(userId, out var profile);

        // Return a copy to simulate an AsNoTracking query.
        var copy = profile is null ? null : Clone(profile);

        return Task.FromResult(copy);
    }

    public Task<Profile?> GetTrackedByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _profiles.TryGetValue(userId, out var profile);

        return Task.FromResult(profile);
    }

    public Task<bool> UsernameExistsAsync(
        string username,
        Guid excludingUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var exists = _profiles.Values.Any(profile =>
            profile.Id != excludingUserId &&
            string.Equals(
                profile.Username,
                username,
                StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(exists);
    }

    public Task<bool> SaveChangesAsync(
    Guid userId,
    CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_profiles.TryGetValue(userId, out var profile))
        {
            return Task.FromResult(false);
        }

        var usernameExists = _profiles.Values.Any(existingProfile =>
            existingProfile.Id != userId &&
            string.Equals(
                existingProfile.Username,
                profile.Username,
                StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(!usernameExists);
    }

    private static Profile Clone(Profile profile)
    {
        return new Profile
        {
            Id = profile.Id,
            Username = profile.Username,
            DisplayName = profile.DisplayName,
            AvatarUrl = profile.AvatarUrl,
            Bio = profile.Bio,
            CreatedAt = profile.CreatedAt,
            UpdatedAt = profile.UpdatedAt
        };
    }
}
