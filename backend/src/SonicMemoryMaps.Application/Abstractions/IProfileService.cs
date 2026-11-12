
using SonicMemoryMaps.Application.Profiles;

namespace SonicMemoryMaps.Application.Abstractions;

public interface IProfileService
{
    Task<ProfileServiceResult<ProfileDto>> GetMyProfileAsync(
        CancellationToken cancellationToken = default);

    Task<ProfileServiceResult<ProfileDto>> UpdateMyProfileAsync(
        UpdateProfileCommand command,
        CancellationToken cancellationToken = default);
}
