using GMSoft.Application.Common.Models;

namespace GMSoft.Application.Common.Interfaces;

/// <summary>Cuentas de usuario. Lo implementa la capa Data sobre Identity.</summary>
public interface IIdentityService
{
    /// <summary>Se entra con el nombre de usuario, no con el email.</summary>
    Task<AuthResult> LoginAsync(string userName, string password, CancellationToken cancellationToken = default);

    /// <summary>Crea la cuenta y le asigna el rol. El email es opcional.</summary>
    Task<Guid> CreateUserAsync(
        string userName,
        string? email,
        string password,
        string firstName,
        string lastName,
        string role,
        CancellationToken cancellationToken = default);

    Task SetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default);

    /// <summary>
    /// Habilita o deshabilita el acceso. Dar de baja a un chofer tiene que cerrarle
    /// la puerta: si no, la ficha desaparece de las listas pero el usuario sigue
    /// entrando y cargando entregas.
    /// </summary>
    Task SetUserActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Nombres de usuario de varias cuentas, para poder mostrarlos en un listado sin
    /// una consulta por fila. El que no aparece es que no tiene cuenta.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> GetUserNamesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);
}
