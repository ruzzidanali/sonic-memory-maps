
namespace SonicMemoryMaps.Application.Abstractions;

public interface IAudioStorageService
{
    Task<AudioUploadAuthorization> CreateUploadAuthorizationAsync(
        Guid userId,
        Guid memoryId,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<AudioObjectMetadata?> GetObjectMetadataAsync(
        Guid userId,
        Guid memoryId,
        string contentType,
        CancellationToken cancellationToken = default);
}

public sealed record AudioUploadAuthorization(
    string ObjectPath,
    string UploadUrl,
    DateTimeOffset ExpiresAt
);
