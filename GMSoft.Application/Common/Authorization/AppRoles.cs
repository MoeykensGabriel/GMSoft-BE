namespace GMSoft.Application.Common.Authorization;

/// <summary>
/// Roles del sistema. Como constantes y no como strings sueltos: un typo en un
/// atributo [Authorize] no da error de compilación, simplemente deja pasar o
/// bloquear a quien no corresponde.
/// </summary>
public static class AppRoles
{
    /// <summary>Dueño y oficina. Ve el negocio y administra el catálogo.</summary>
    public const string Admin = "Admin";

    /// <summary>Chofer. Abre su sesión de reparto y carga entregas.</summary>
    public const string Driver = "Driver";

    /// <summary>
    /// Para endpoints que los dos necesitan. El chofer tiene que poder leer el
    /// catálogo para cargar el camión y registrar entregas, aunque no pueda tocarlo.
    /// </summary>
    public const string AdminOrDriver = Admin + "," + Driver;

    /// <summary>
    /// Controlador. Se sube a un camión a contar envases y confirmar clientes. No vende,
    /// no cobra y no ve plata: solo entra a los endpoints de control.
    /// </summary>
    public const string Inspector = "Inspector";

    public static readonly IReadOnlyList<string> All = [Admin, Driver, Inspector];
}
