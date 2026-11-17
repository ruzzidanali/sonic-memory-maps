namespace SonicMemoryMaps.Application.Memories;

public sealed record CreateAudioUploadCommand(
    string RequestedAudioContentType
);