using GMSoft.Application.Common.Models;

namespace GMSoft.Application.Common.Interfaces;

/// <summary>Emite el JWT. Lo implementa la capa Data.</summary>
public interface IJwtTokenService
{
    string GenerateToken(AuthUserData user);
}

/// <summary>Datos que necesita el token. Sin tipos de Identity: Application no los conoce.</summary>
public record AuthUserData(
    Guid                  UserId,
    string                UserName,
    string?               Email,
    IReadOnlyList<string> Roles,
    Guid?                 DriverId);
