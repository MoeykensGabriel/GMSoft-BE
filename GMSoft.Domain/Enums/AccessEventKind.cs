namespace GMSoft.Domain.Enums;

/// <summary>Qué pasó con el acceso de un chofer a la app.</summary>
public enum AccessEventKind
{
    /// <summary>Entró con usuario y contraseña.</summary>
    Login = 1,

    /// <summary>La app cerró la sesión y avisó por qué.</summary>
    Logout = 2,

    /// <summary>El servidor rechazó su acceso (vencido, contraseña cambiada, cuenta desactivada).</summary>
    Rejected = 3
}
