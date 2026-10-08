using GMSoft.Application.Common;
using GMSoft.Application.Features.Deliveries.Register;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;

namespace GMSoft.Application.Features.Promotions.Common;

public record PromotionLineDto(Guid ProductId, string ProductDetail, int Quantity,
    int ContainersLoaned, int ContainersReturned, int ContainersLost);
public record PromotionDto(Guid Id, Guid VehicleId, string VehicleName, string VehicleLicensePlate,
    Guid DriverId, string DriverName, Guid DeliverySessionId, Guid ZoneId,
    DateTime RegisteredAt, DateOnly PickupDate, string Status, bool IsOverdue, bool IsDueToday,
    NewCustomerLine Prospect, IReadOnlyList<PromotionLineDto> Lines, Guid? CustomerId,
    DateTime? ClosedAt, Guid? ClosingSessionId, Guid? ClosedByDriverId,
    Guid ClientRequestId, Guid? CloseClientRequestId)
{
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.Add(BusinessTime.Offset));
    public static PromotionDto From(Promotion p)
    {
        var today = Today;
        var overdue = p.Status == PromotionStatus.Pending && p.PickupDate < today;
        return new(p.Id, p.VehicleId, p.Vehicle?.Name ?? "", p.Vehicle?.LicensePlate ?? "",
            p.DriverId, p.Driver is null ? "" : $"{p.Driver.FirstName} {p.Driver.LastName}",
            p.DeliverySessionId, p.ZoneId, p.RegisteredAt, p.PickupDate,
            overdue ? "Overdue" : p.Status.ToString(), overdue,
            p.Status == PromotionStatus.Pending && p.PickupDate == today,
            new(p.BusinessName, p.ContactName, p.Phone, p.Address, p.Notes, p.VisitDays),
            p.Lines.OrderBy(l => l.ProductId).Select(l => new PromotionLineDto(l.ProductId,
                l.Product?.Detail ?? "", l.Quantity, l.ContainersLoaned, l.ContainersReturned, l.ContainersLost)).ToList(),
            p.CustomerId, p.ClosedAt, p.ClosingSessionId, p.ClosedByDriverId,
            p.ClientRequestId, p.CloseClientRequestId);
    }
}

public record PromotionResult(Guid PromotionId, Guid? CustomerId, string Status,
    DateTime RegisteredAt, DateOnly PickupDate, DateTime? ClosedAt)
{
    public static PromotionResult From(Promotion p) =>
        new(p.Id, p.CustomerId, p.Status.ToString(), p.RegisteredAt, p.PickupDate, p.ClosedAt);
}

