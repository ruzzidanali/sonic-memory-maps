
using SonicMemoryMaps.Application.Abstractions;
using SonicMemoryMaps.Domain.Entities;

namespace SonicMemoryMaps.Application.Profiles;

public sealed class ProfileService(
    IProfileRepository profileRepository,
    ICurrentUser currentUser) : IProfileService
{
    public async Task<ProfileServiceResult<ProfileDto>> GetMyProfileAsync(
        CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not Guid userId)
        {
            return new(ProfileServiceStatus.Unauthorized);
        }

        var profile = await profileRepository.GetByIdAsync(
            userId,
            cancellationToken);

        if (profile is null)
        {
            return new(ProfileServiceStatus.NotFound);
        }

        return new(
            ProfileServiceStatus.Success,
            MapToDto(profile));
    }

    public async Task<ProfileServiceResult<ProfileDto>> UpdateMyProfileAsync(
        UpdateProfileCommand command,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not Guid userId)
        {
            return new(ProfileServiceStatus.Unauthorized);
        }

        var username = command.Username.Trim();

        if (command.DisplayName?.Trim().Length > 100)
        {
            return new(
                ProfileServiceStatus.Invalid,
                Error: "Display name cannot exceed 100 characters.");
        }

        if (command.Bio?.Trim().Length > 300)
        {
            return new(
                ProfileServiceStatus.Invalid,
                Error: "Bio cannot exceed 300 characters.");
        }

        if (username.Length is < 3 or > 30)
        {
            return new(
                ProfileServiceStatus.Invalid,
                Error: "Username must contain between 3 and 30 characters.");
        }

        var profile = await profileRepository.GetTrackedByIdAsync(
            userId,
            cancellationToken);

        if (profile is null)
        {
            return new(ProfileServiceStatus.NotFound);
        }

        profile.Username = username;
        profile.DisplayName = NormalizeOptionalText(command.DisplayName);
        profile.Bio = NormalizeOptionalText(command.Bio);
        profile.UpdatedAt = DateTimeOffset.UtcNow;

        var saved = await profileRepository.SaveChangesAsync(
            userId,
            cancellationToken);

        if (!saved)
        {
            return new(
                ProfileServiceStatus.Conflict,
                Error: "This username is already taken.");
        }

        return new(
            ProfileServiceStatus.Success,
            MapToDto(profile));
    }

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static ProfileDto MapToDto(Profile profile)
    {
        return new ProfileDto(
            profile.Id,
            profile.Username,
            profile.DisplayName,
            profile.AvatarUrl,
            profile.Bio,
            profile.CreatedAt,
            profile.UpdatedAt);
    }
}
