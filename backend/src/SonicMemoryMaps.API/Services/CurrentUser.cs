using System.Security.Claims;
using SonicMemoryMaps.Application.Abstractions;

namespace SonicMemoryMaps.API.Services;

public sealed class CurrentUser(
    IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? User =>
        httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated =>
        User?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId
    {
        get
        {
            var subject = User?.FindFirst("sub")?.Value;

            return Guid.TryParse(subject, out var userId)
                ? userId
                : null;
        }
    }

    public string? Email =>
        User?.FindFirst("email")?.Value;
}