namespace GMSoft.Application.Common.Interfaces;

/// <summary>
/// Guarda la contraseña del chofer cifrada para que la oficina la pueda ver en la lista
/// de choferes (pedido del dueño, 23/10). No reemplaza al hash de Identity, que sigue
/// siendo lo único que se usa para entrar.
/// </summary>
public interface IPasswordVault
{
    string Protect(string password);

    /// <summary>La contraseña, o nula si no hay o si se cifró con otra clave.</summary>
    string? Reveal(string? cipher);
}
