using SonicMemoryMaps.Application.Abstractions;

namespace SonicMemoryMaps.Application.Memories;

public sealed class AudioUploadService(
    ICurrentUser currentUser,
    IAudioStorageService audioStorageService) : IAudioUploadService
{
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

    public async Task<AudioUploadServiceResult> CreateUploadAuthorizationAsync(
        CreateAudioUploadCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid userId)
        {
            return new AudioUploadServiceResult(
                AudioUploadServiceStatus.Unauthorized,
                Error: "Authentication is required.");
        }

        if (string.IsNullOrWhiteSpace(command.RequestedAudioContentType) ||
            !SupportedAudioContentTypes.Contains(command.RequestedAudioContentType))
        {
            return new AudioUploadServiceResult(
                AudioUploadServiceStatus.Invalid,
                Error: "Unsupported audio content type.");
        }

        var memoryId = Guid.NewGuid();

        var authorization =
            await audioStorageService.CreateUploadAuthorizationAsync(
                userId,
                memoryId,
                command.RequestedAudioContentType,
                cancellationToken);

        var data = new AudioUploadDto(
            memoryId,
            authorization.ObjectPath,
            authorization.UploadUrl,
            authorization.ExpiresAt);

        return new AudioUploadServiceResult(
            AudioUploadServiceStatus.Success,
            Data: data);
    }
}