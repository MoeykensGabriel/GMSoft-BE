using System.Security.Claims;
using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Interfaces;

namespace GMSoft.API.Services;

/// <summary>
/// Lee del JWT quién está haciendo el request. Vive en la API porque es la única
/// capa que conoce el HttpContext.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public Guid? UserId => ParseGuid(Principal?.FindFirst(AppClaimTypes.UserId)?.Value);

    public string? UserName => Principal?.FindFirst(AppClaimTypes.UserName)?.Value;

    public string? Email => Principal?.FindFirst(AppClaimTypes.Email)?.Value;

    public Guid? DriverId => ParseGuid(Principal?.FindFirst(AppClaimTypes.DriverId)?.Value);

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;

    private static Guid? ParseGuid(string? value)
        => Guid.TryParse(value, out var parsed) ? parsed : null;
}
