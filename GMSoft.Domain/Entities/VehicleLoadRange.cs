namespace GMSoft.Domain.Entities;

public class VehicleLoadRange
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VehicleId { get; set; }
    public Guid ProductId { get; set; }
    public int? Min { get; set; }
    public int? Max { get; set; }
}
