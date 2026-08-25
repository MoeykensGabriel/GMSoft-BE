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

    public static readonly IReadOnlyList<string> All = [Admin, Driver];
}
