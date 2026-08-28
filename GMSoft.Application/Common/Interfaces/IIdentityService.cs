using GMSoft.Application.Common.Models;

namespace GMSoft.Application.Common.Interfaces;

/// <summary>Cuentas de usuario. Lo implementa la capa Data sobre Identity.</summary>
public interface IIdentityService
{
    Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default);

    /// <summary>Crea la cuenta y le asigna el rol. Devuelve el id del usuario.</summary>
    Task<Guid> CreateUserAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        string role,
        CancellationToken cancellationToken = default);

    /// <summary>Le pone una contraseña nueva sin pedir la anterior: la asigna el admin.</summary>
    Task SetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default);

    /// <summary>
    /// Habilita o deshabilita el acceso. Dar de baja a un chofer tiene que cerrarle
    /// la puerta: si no, la ficha desaparece de las listas pero el usuario sigue
    /// entrando y cargando entregas.
    /// </summary>
    Task SetUserActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default);
}
