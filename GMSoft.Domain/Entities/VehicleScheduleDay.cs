namespace GMSoft.Domain.Entities;

public class VehicleScheduleDay
{
    public Guid VehicleId { get; set; }
    public int Day { get; set; }
    /// <summary>Orden de la zona en el día: 0 es la principal. Un día sin salida es una sola fila sin zona.</summary>
    public int Position { get; set; }
    public Guid? ZoneId { get; set; }
    public Zone? Zone { get; set; }
}
