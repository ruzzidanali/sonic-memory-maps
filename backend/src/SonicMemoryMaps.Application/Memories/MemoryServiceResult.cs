namespace SonicMemoryMaps.Application.Memories;

public enum MemoryServiceStatus
{
    Success,
    Unauthorized,
    NotFound,
    Conflict,
    Invalid
}

public sealed record MemoryServiceResult<T>(
    MemoryServiceStatus Status,
    T? Data = default,
    string? Error = null
);