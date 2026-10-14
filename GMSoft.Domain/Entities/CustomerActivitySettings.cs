namespace GMSoft.Domain.Entities;

/// <summary>
/// Umbrales de actividad elegidos por ADMIN. Una sola fila; si no existe, valen los
/// de la configuración de la API.
/// </summary>
public class CustomerActivitySettings
{
    public int Id { get; set; } = 1;
    public int RedAfterMissedWeeks { get; set; }
    public int BlackAfterMissedWeeks { get; set; }
}
