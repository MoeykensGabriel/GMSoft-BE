namespace GMSoft.Application.Common.Models;

/// <summary>Lo que devuelve un login exitoso.</summary>
public record AuthResult(
    string                   Token,
    Guid                     UserId,
    string                   Email,
    string                   FullName,
    IReadOnlyList<string>    Roles,
    Guid?                    DriverId);
