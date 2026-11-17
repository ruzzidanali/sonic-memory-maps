namespace SonicMemoryMaps.Application.Memories;

public enum AudioUploadServiceStatus
{
    Success,
    Unauthorized,
    Invalid
}

public sealed record AudioUploadServiceResult(
    AudioUploadServiceStatus Status,
    AudioUploadDto? Data = null,
    string? Error = null
);