using Microsoft.AspNetCore.Identity;

namespace GMSoft.Data.Identity;

/// <summary>
/// Cuenta con la que se entra al sistema. Vive en Data porque depende de Identity;
/// el Domain no la conoce y se ata a ella por Guid (ver Driver.ApplicationUserId).
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Baja de la cuenta sin borrarla. Un chofer que se fue no puede entrar, pero
    /// sus sesiones y entregas siguen apuntando a él.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
