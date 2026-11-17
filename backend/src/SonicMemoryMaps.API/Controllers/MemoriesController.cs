
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SonicMemoryMaps.API.Contracts.Memories;
using SonicMemoryMaps.API.DTOs.Memories;
using SonicMemoryMaps.Application.Abstractions;
using SonicMemoryMaps.Application.Memories;

namespace SonicMemoryMaps.API.Controllers;

[ApiController]
[Route("api/memories")]
[Authorize]
public sealed class MemoriesController(
    IMemoryService memoryService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<MemoryResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateMemory(
        [FromBody] CreateMemoryRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateMemoryCommand(
            request.MemoryId,
            request.Title,
            request.Description,
            request.RequestedAudioContentType,
            request.AudioDurationSeconds,
            request.Latitude,
            request.Longitude,
            request.Visibility,
            request.NotificationRadiusMeters);

        var result = await memoryService.CreateMemoryAsync(
            command,
            cancellationToken);

        if (result.Status != MemoryServiceStatus.Success)
        {
            return MapError(result);
        }

        var response = MapResponse(result.Data!);

        return StatusCode(
            StatusCodes.Status201Created,
            response);
    }


    [HttpGet("me")]
    [ProducesResponseType<IEnumerable<MemoryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyMemories(
        CancellationToken cancellationToken)
    {
        var result = await memoryService.GetMyMemoriesAsync(
            cancellationToken);

        if (result.Status == MemoryServiceStatus.Unauthorized)
        {
            return Unauthorized();
        }

        var response = result.Data!
            .Select(MapResponse)
            .ToList();

        return Ok(response);
    }


    [HttpGet("{id:guid}")]
    [ProducesResponseType<MemoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMemoryById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await memoryService.GetMemoryByIdAsync(
            id,
            cancellationToken);

        if (result.Status != MemoryServiceStatus.Success)
        {
            return MapError(result);
        }

        return Ok(MapResponse(result.Data!));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<MemoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMemory(
    Guid id,
    [FromBody] UpdateMemoryRequest request,
    CancellationToken cancellationToken)
    {
        var command = new UpdateMemoryCommand(
            id,
            request.Title,
            request.Description,
            request.Visibility,
            request.NotificationRadiusMeters);

        var result = await memoryService.UpdateMemoryAsync(
            command,
            cancellationToken);

        if (result.Status != MemoryServiceStatus.Success)
        {
            return MapError(result);
        }

        return Ok(MapResponse(result.Data!));
    }


    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMemory(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await memoryService.DeleteMemoryAsync(
            id,
            cancellationToken);

        if (result.Status != MemoryServiceStatus.Success)
        {
            return MapDeleteError(result);
        }

        return NoContent();
    }

    private IActionResult MapDeleteError(
        MemoryServiceResult<bool> result)
    {
        return result.Status switch
        {
            MemoryServiceStatus.Unauthorized =>
                Unauthorized(),

            MemoryServiceStatus.NotFound =>
                NotFound(new ProblemDetails
                {
                    Title = "Memory not found",
                    Detail = result.Error,
                    Status = StatusCodes.Status404NotFound
                }),

            MemoryServiceStatus.Invalid =>
                BadRequest(new ProblemDetails
                {
                    Title = "Invalid memory information",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                }),

            _ => StatusCode(
                StatusCodes.Status500InternalServerError)
        };
    }

    private IActionResult MapError(
        MemoryServiceResult<MemoryDto> result)
    {
        return result.Status switch
        {
            MemoryServiceStatus.Unauthorized =>
                Unauthorized(),

            MemoryServiceStatus.NotFound =>
                NotFound(new ProblemDetails
                {
                    Title = "Uploaded audio not found",
                    Detail = result.Error,
                    Status = StatusCodes.Status404NotFound
                }),

            MemoryServiceStatus.Invalid =>
                BadRequest(new ProblemDetails
                {
                    Title = "Invalid memory information",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                }),

            MemoryServiceStatus.Conflict =>
                Conflict(new ProblemDetails
                {
                    Title = "Memory conflict",
                    Detail = result.Error,
                    Status = StatusCodes.Status409Conflict
                }),

            _ => StatusCode(
                StatusCodes.Status500InternalServerError)
        };
    }

    private static MemoryResponse MapResponse(MemoryDto memory)
    {
        return new MemoryResponse(
            memory.Id,
            memory.UserId,
            memory.Title,
            memory.Description,
            memory.AudioDurationSeconds,
            memory.AudioContentType,
            memory.AudioSizeBytes,
            memory.Latitude,
            memory.Longitude,
            memory.Visibility,
            memory.NotificationRadiusMeters,
            memory.ModerationStatus,
            memory.CreatedAt,
            memory.UpdatedAt);
    }
}
