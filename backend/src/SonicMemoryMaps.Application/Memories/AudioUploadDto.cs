namespace SonicMemoryMaps.Application.Memories;

public sealed record AudioUploadDto(
    Guid MemoryId,
    string ObjectPath,
    string UploadUrl,
    DateTimeOffset ExpiresAt
);