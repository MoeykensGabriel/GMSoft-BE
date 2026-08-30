namespace GMSoft.Application.Common.Interfaces;

/// <summary>
/// El usuario que hizo el request, leído de los claims del JWT. Lo implementa la
/// capa API, que es la única que conoce el HttpContext.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }

    /// <summary>Con lo que entro al sistema.</summary>
    string? UserName { get; }

    /// <summary>Contacto. Puede no tener.</summary>
    string? Email { get; }

    /// <summary>Perfil de chofer, si el usuario tiene uno.</summary>
    Guid? DriverId { get; }

    bool IsInRole(string role);
}
