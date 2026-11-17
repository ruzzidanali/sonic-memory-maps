using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SonicMemoryMaps.API.Contracts.AudioUploads;
using SonicMemoryMaps.API.DTOs.AudioUploads;
using SonicMemoryMaps.Application.Abstractions;
using SonicMemoryMaps.Application.Memories;

namespace SonicMemoryMaps.API.Controllers;

[ApiController]
[Route("api/audio-uploads")]
[Authorize]
public sealed class AudioUploadsController(
    IAudioUploadService audioUploadService) : ControllerBase
{
    [HttpPost("authorization")]
    [ProducesResponseType<AudioUploadResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateUploadAuthorization(
        [FromBody] CreateAudioUploadRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateAudioUploadCommand(
            request.RequestedAudioContentType);

        var result = await audioUploadService.CreateUploadAuthorizationAsync(
            command,
            cancellationToken);

        if (result.Status != AudioUploadServiceStatus.Success)
        {
            return MapError(result);
        }

        var response = new AudioUploadResponse(
            result.Data!.MemoryId,
            result.Data.ObjectPath,
            result.Data.UploadUrl,
            result.Data.ExpiresAt);

        return Ok(response);
    }

    private IActionResult MapError(AudioUploadServiceResult result)
    {
        return result.Status switch
        {
            AudioUploadServiceStatus.Unauthorized =>
                Unauthorized(),

            AudioUploadServiceStatus.Invalid =>
                BadRequest(new ProblemDetails
                {
                    Title = "Invalid audio upload request",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                }),

            _ => StatusCode(
                StatusCodes.Status500InternalServerError)
        };
    }
}