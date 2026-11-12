using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SonicMemoryMaps.API.DTOs.Profiles;
using SonicMemoryMaps.Application.Abstractions;
using SonicMemoryMaps.Application.Profiles;

namespace SonicMemoryMaps.API.Controllers;

[ApiController]
[Route("api/profiles")]
[Authorize]
public sealed class ProfilesController(
    IProfileService profileService) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyProfile(
        CancellationToken cancellationToken)
    {
        var result = await profileService.GetMyProfileAsync(
            cancellationToken);

        if (result.Status != ProfileServiceStatus.Success)
        {
            return MapError(result);
        }

        return Ok(MapResponse(result.Data!));
    }

    [HttpPut("me")]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateMyProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateProfileCommand(
            request.Username,
            request.DisplayName,
            request.Bio);

        var result = await profileService.UpdateMyProfileAsync(
            command,
            cancellationToken);

        if (result.Status != ProfileServiceStatus.Success)
        {
            return MapError(result);
        }

        return Ok(MapResponse(result.Data!));
    }

    private IActionResult MapError(
        ProfileServiceResult<ProfileDto> result)
    {
        return result.Status switch
        {
            ProfileServiceStatus.Unauthorized =>
                Unauthorized(),

            ProfileServiceStatus.NotFound =>
                NotFound(new ProblemDetails
                {
                    Title = "Profile not found",
                    Status = StatusCodes.Status404NotFound
                }),

            ProfileServiceStatus.Conflict =>
                Conflict(new ProblemDetails
                {
                    Title = "Profile conflict",
                    Detail = result.Error,
                    Status = StatusCodes.Status409Conflict
                }),

            ProfileServiceStatus.Invalid =>
                BadRequest(new ProblemDetails
                {
                    Title = "Invalid profile information",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                }),

            _ => StatusCode(
                StatusCodes.Status500InternalServerError)
        };
    }

    private static ProfileResponse MapResponse(ProfileDto profile)
    {
        return new ProfileResponse(
            profile.Id,
            profile.Username,
            profile.DisplayName,
            profile.AvatarUrl,
            profile.Bio,
            profile.CreatedAt,
            profile.UpdatedAt);
    }
}