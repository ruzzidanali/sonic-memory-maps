namespace SonicMemoryMaps.API.DTOs.AudioUploads;

public sealed record AudioUploadResponse(
    Guid MemoryId,
    string ObjectPath,
    string UploadUrl,
    DateTimeOffset ExpiresAt
);