using GMSoft.Domain.Common;

namespace GMSoft.Domain.Entities;

public class VehicleKilometersCorrection : BaseEntity
{
    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public int PreviousKilometers { get; set; }
    public int NewKilometers { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CorrectedAt { get; set; }
    public Guid? CorrectedByUserId { get; set; }
}
