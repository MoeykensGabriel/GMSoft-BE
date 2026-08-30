namespace GMSoft.Application.Common.Models;

/// <summary>Lo que devuelve un login exitoso.</summary>
public record AuthResult(
    string                Token,
    Guid                  UserId,

    /// <summary>Con esto entra al sistema. Es la credencial.</summary>
    string                UserName,

    /// <summary>Dato de contacto, no credencial. Puede no tener.</summary>
    string?               Email,

    string                FullName,
    IReadOnlyList<string> Roles,
    Guid?                 DriverId);
