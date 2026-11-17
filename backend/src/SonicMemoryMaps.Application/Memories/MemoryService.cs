
using NetTopologySuite.Geometries;
using SonicMemoryMaps.Application.Abstractions;
using SonicMemoryMaps.Domain.Entities;
using SonicMemoryMaps.Domain.Enums;

namespace SonicMemoryMaps.Application.Memories;

public sealed class MemoryService(
    IMemoryRepository memoryRepository,
    ICurrentUser currentUser,
    IAudioStorageService audioStorageService) : IMemoryService
{
    private const long MaxAudioSizeBytes = 52_428_800;

    private static readonly HashSet<string> SupportedAudioContentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "audio/mp4",
            "audio/mpeg",
            "audio/aac",
            "audio/wav",
            "audio/ogg",
            "audio/webm"
        };

    public async Task<MemoryServiceResult<MemoryDto>> CreateMemoryAsync(
        CreateMemoryCommand command,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not Guid userId ||
            !currentUser.IsAuthenticated)
        {
            return new(MemoryServiceStatus.Unauthorized);
        }

        if (command.MemoryId == Guid.Empty)
        {
            return new(
                MemoryServiceStatus.Invalid,
                Error: "A valid memory ID is required.");
        }

        var title = command.Title?.Trim();

        if (string.IsNullOrWhiteSpace(title) || title.Length > 100)
        {
            return new(
                MemoryServiceStatus.Invalid,
                Error: "Title must contain between 1 and 100 characters.");
        }

        if (command.Description?.Trim().Length > 500)
        {
            return new(
                MemoryServiceStatus.Invalid,
                Error: "Description cannot exceed 500 characters.");
        }

        if (command.AudioDurationSeconds is < 1 or > 600)
        {
            return new(
                MemoryServiceStatus.Invalid,
                Error: "Audio duration must be between 1 and 600 seconds.");
        }

        if (!SupportedAudioContentTypes.Contains(
            command.RequestedAudioContentType))
        {
            return new(
                MemoryServiceStatus.Invalid,
                Error: "Unsupported audio content type.");
        }

        if (!double.IsFinite(command.Latitude) ||
            command.Latitude is < -90 or > 90)
        {
            return new(
                MemoryServiceStatus.Invalid,
                Error: "Latitude must be between -90 and 90.");
        }

        if (!double.IsFinite(command.Longitude) ||
            command.Longitude is < -180 or > 180)
        {
            return new(
                MemoryServiceStatus.Invalid,
                Error: "Longitude must be between -180 and 180.");
        }

        if (!Enum.IsDefined(command.Visibility))
        {
            return new(
                MemoryServiceStatus.Invalid,
                Error: "Invalid memory visibility.");
        }

        if (command.NotificationRadiusMeters is < 10 or > 250)
        {
            return new(
                MemoryServiceStatus.Invalid,
                Error: "Notification radius must be between 10 and 250 metres.");
        }

        // Retrieve trusted metadata from Supabase Storage.
        var audioMetadata =
            await audioStorageService.GetObjectMetadataAsync(
                userId,
                command.MemoryId,
                command.RequestedAudioContentType,
                cancellationToken);

        if (audioMetadata is null)
        {
            return new(
                MemoryServiceStatus.NotFound,
                Error: "The uploaded audio file could not be found.");
        }

        if (audioMetadata.SizeBytes is < 1 or > MaxAudioSizeBytes)
        {
            return new(
                MemoryServiceStatus.Invalid,
                Error: "Audio size must be between 1 byte and 50 MB.");
        }

        if (!string.Equals(
            audioMetadata.ContentType,
            command.RequestedAudioContentType,
            StringComparison.OrdinalIgnoreCase))
        {
            return new(
                MemoryServiceStatus.Invalid,
                Error: "Uploaded audio content type does not match the requested content type.");
        }

        var expectedExtension = command.RequestedAudioContentType.ToLowerInvariant() switch
        {
            "audio/mp4" => ".m4a",
            "audio/mpeg" => ".mp3",
            "audio/aac" => ".aac",
            "audio/wav" => ".wav",
            "audio/ogg" => ".ogg",
            "audio/webm" => ".webm",
            _ => string.Empty
        };

        var expectedObjectPath =
            $"{userId:D}/{command.MemoryId:D}{expectedExtension}";

        if (!string.Equals(
            audioMetadata.ObjectPath,
            expectedObjectPath,
            StringComparison.Ordinal))
        {
            return new(
                MemoryServiceStatus.Invalid,
                Error: "Uploaded audio object path is invalid.");
        }

        var memory = new Memory
        {
            Id = command.MemoryId,
            UserId = userId,
            Title = title,
            Description = NormalizeOptionalText(command.Description),
            AudioPath = audioMetadata.ObjectPath,
            AudioDurationSeconds = command.AudioDurationSeconds,
            AudioContentType = audioMetadata.ContentType,
            AudioSizeBytes = audioMetadata.SizeBytes,
            Location = new Point(
                command.Longitude,
                command.Latitude)
            {
                SRID = 4326
            },
            Visibility = command.Visibility,
            NotificationRadiusMeters = command.NotificationRadiusMeters,
            ModerationStatus = ModerationStatus.Pending
        };

        await memoryRepository.AddAsync(
            memory,
            userId,
            cancellationToken);

        return new(
            MemoryServiceStatus.Success,
            MapToDto(memory));
    }


    public async Task<MemoryServiceResult<IReadOnlyList<MemoryDto>>> GetMyMemoriesAsync(
        CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not Guid userId ||
            !currentUser.IsAuthenticated)
        {
            return new(MemoryServiceStatus.Unauthorized);
        }

        var memories = await memoryRepository.GetByOwnerAsync(
            userId,
            cancellationToken);

        var memoryDtos = memories
            .Select(MapToDto)
            .ToList();

        return new(
            MemoryServiceStatus.Success,
            memoryDtos);
    }

    public async Task<MemoryServiceResult<MemoryDto>> GetMemoryByIdAsync(
    Guid memoryId,
    CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not Guid userId ||
            !currentUser.IsAuthenticated)
        {
            return new(MemoryServiceStatus.Unauthorized);
        }

        if (memoryId == Guid.Empty)
        {
            return new(
                MemoryServiceStatus.Invalid,
                Error: "A valid memory ID is required.");
        }

        var memory = await memoryRepository.GetByIdForOwnerAsync(
            memoryId,
            userId,
            cancellationToken);

        if (memory is null)
        {
            return new(
                MemoryServiceStatus.NotFound,
                Error: "Memory was not found.");
        }

        return new(
            MemoryServiceStatus.Success,
            MapToDto(memory));
    }


    public async Task<MemoryServiceResult<MemoryDto>> UpdateMemoryAsync(
        UpdateMemoryCommand command,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not Guid userId ||
            !currentUser.IsAuthenticated)
        {
            return new(MemoryServiceStatus.Unauthorized);
        }

        if (command.MemoryId == Guid.Empty)
        {
            return new(
                MemoryServiceStatus.Invalid,
                Error: "A valid memory ID is required.");
        }

        var title = command.Title?.Trim();

        if (string.IsNullOrWhiteSpace(title) || title.Length > 100)
        {
            return new(
                MemoryServiceStatus.Invalid,
                Error: "Title must contain between 1 and 100 characters.");
        }

        if (command.Description?.Trim().Length > 500)
        {
            return new(
                MemoryServiceStatus.Invalid,
                Error: "Description cannot exceed 500 characters.");
        }

        if (!Enum.IsDefined(command.Visibility))
        {
            return new(
                MemoryServiceStatus.Invalid,
                Error: "Invalid memory visibility.");
        }

        if (command.NotificationRadiusMeters is < 10 or > 250)
        {
            return new(
                MemoryServiceStatus.Invalid,
                Error: "Notification radius must be between 10 and 250 metres.");
        }

        var memory = await memoryRepository.GetTrackedByIdForOwnerAsync(
            command.MemoryId,
            userId,
            cancellationToken);

        if (memory is null)
        {
            return new(
                MemoryServiceStatus.NotFound,
                Error: "Memory was not found.");
        }

        memory.Title = title;
        memory.Description = NormalizeOptionalText(command.Description);
        memory.Visibility = command.Visibility;
        memory.NotificationRadiusMeters = command.NotificationRadiusMeters;
        memory.UpdatedAt = DateTimeOffset.UtcNow;

        await memoryRepository.SaveChangesAsync(
            userId,
            cancellationToken);

        return new(
            MemoryServiceStatus.Success,
            MapToDto(memory));
    }

    public async Task<MemoryServiceResult<bool>> DeleteMemoryAsync(
    Guid memoryId,
    CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not Guid userId ||
            !currentUser.IsAuthenticated)
        {
            return new(MemoryServiceStatus.Unauthorized);
        }

        if (memoryId == Guid.Empty)
        {
            return new(
                MemoryServiceStatus.Invalid,
                Error: "A valid memory ID is required.");
        }

        var memory = await memoryRepository.GetTrackedByIdForOwnerAsync(
            memoryId,
            userId,
            cancellationToken);

        if (memory is null)
        {
            return new(
                MemoryServiceStatus.NotFound,
                Error: "Memory was not found.");
        }

        var now = DateTimeOffset.UtcNow;

        memory.DeletedAt = now;
        memory.UpdatedAt = now;

        await memoryRepository.SaveChangesAsync(
            userId,
            cancellationToken);

        return new(
            MemoryServiceStatus.Success,
            true);
    }



    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim();
    }

    private static MemoryDto MapToDto(Memory memory)
    {
        return new MemoryDto(
            memory.Id,
            memory.UserId,
            memory.Title,
            memory.Description,
            memory.AudioDurationSeconds,
            memory.AudioContentType,
            memory.AudioSizeBytes,
            memory.Location.Y,
            memory.Location.X,
            memory.Visibility,
            memory.NotificationRadiusMeters,
            memory.ModerationStatus,
            memory.CreatedAt,
            memory.UpdatedAt);
    }
}
