
namespace SonicMemoryMaps.Application.Abstractions;

public sealed record AudioObjectMetadata(
    string ObjectPath,
    string ContentType,
    long SizeBytes
);
