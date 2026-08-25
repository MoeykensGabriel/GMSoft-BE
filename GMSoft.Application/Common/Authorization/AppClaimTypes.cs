namespace GMSoft.Application.Common.Authorization;

/// <summary>
/// Nombres de los claims del JWT. Los emite la capa Data y los lee la API, así
/// que viven acá para que las dos puntas usen exactamente la misma cadena.
/// </summary>
public static class AppClaimTypes
{
    public const string UserId = "sub";
    public const string Email  = "email";
    public const string Role   = "role";

    /// <summary>
    /// Perfil de chofer del usuario, cuando lo tiene. Evita que cada endpoint del
    /// reparto tenga que ir a buscar el Driver por el id de usuario.
    /// </summary>
    public const string DriverId = "driverId";
}
