namespace GMSoft.Domain.Entities;

/// <summary>
/// Una de las zonas que recorre la salida, en el orden en que la recorre. La primera
/// (posición 0) es la misma que <see cref="DeliverySession.ZoneId"/>: la zona principal
/// que siguen usando los listados y lo que necesita una sola.
/// </summary>
public class DeliverySessionZone
{
    public Guid DeliverySessionId { get; set; }
    public Guid ZoneId { get; set; }
    public Zone Zone { get; set; } = null!;
    public int Position { get; set; }
}
