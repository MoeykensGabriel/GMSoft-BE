using GMSoft.Domain.Common;

namespace GMSoft.Domain.Entities;

public enum RemovalStatus { Pending, Confirmed, Rejected }
public enum RemovalReason { Moved, ClosedBusiness, NoLongerWants, Deceased, Other }

public class CustomerRemovalRequest : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public Guid ClientRequestId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public Guid DriverId { get; set; }
    public Driver Driver { get; set; } = null!;
    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public Guid ZoneId { get; set; }
    public Zone Zone { get; set; } = null!;
    public DateTime RequestedAt { get; set; }
    public RemovalStatus Status { get; set; }
    public RemovalReason Reason { get; set; }
    public string? Notes { get; set; }
    public bool ContainersNotReturned { get; set; }
    public decimal Debt { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public List<RemovalContainerSnapshot> Containers { get; set; } = [];
}

public class RemovalContainerSnapshot
{
    public Guid ProductId { get; set; }
    public string ProductDetail { get; set; } = "";
    public int Quantity { get; set; }
}
