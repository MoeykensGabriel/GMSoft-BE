using GMSoft.Domain.Common;

namespace GMSoft.Domain.Entities;

public class RouteOrderChange : BaseEntity
{
    public Guid VehicleId { get; set; }
    public Guid ZoneId { get; set; }
    public Guid? DeliverySessionId { get; set; }
    public Guid? ChangedByUserId { get; set; }
    public DateTime ChangedAt { get; set; }
    public int MovedCount { get; set; }
}
