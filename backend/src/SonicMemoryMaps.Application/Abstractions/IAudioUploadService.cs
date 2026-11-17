using SonicMemoryMaps.Application.Memories;

namespace SonicMemoryMaps.Application.Abstractions;

public interface IAudioUploadService
{
    Task<AudioUploadServiceResult> CreateUploadAuthorizationAsync(
        CreateAudioUploadCommand command,
        CancellationToken cancellationToken = default);
}