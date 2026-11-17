namespace SonicMemoryMaps.API.Contracts.AudioUploads;

public sealed record CreateAudioUploadRequest(
    string RequestedAudioContentType
);