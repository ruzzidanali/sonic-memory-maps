
namespace SonicMemoryMaps.Application.Profiles;

public enum ProfileServiceStatus
{
    Success,
    Unauthorized,
    NotFound,
    Conflict,
    Invalid
}

public sealed record ProfileServiceResult<T>(
    ProfileServiceStatus Status,
    T? Data = default,
    string? Error = null
);
