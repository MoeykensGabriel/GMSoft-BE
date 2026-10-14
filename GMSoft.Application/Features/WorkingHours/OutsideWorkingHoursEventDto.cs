using GMSoft.Domain.Entities;

namespace GMSoft.Application.Features.WorkingHours;

public record OutsideWorkingHoursEventDto(string Operation, Guid Id, DateTime OccurredAt,
    Guid? UserId, Guid? DriverId)
{
    public static IReadOnlyList<OutsideWorkingHoursEventDto> From(DeliverySession s)
    {
        var events = new List<OutsideWorkingHoursEventDto>();
        if (s.OpenedOutsideWorkingHours) events.Add(new("session-open", s.Id, s.OpenedAt, s.OpenedByUserId, s.DriverId));
        if (s.ClosedOutsideWorkingHours && s.ClosedAt is { } closed)
            events.Add(new("session-close", s.Id, closed, s.ClosedByUserId, null));
        if (s.CashSettlement is { IsOutsideWorkingHours: true } settlement)
            events.Add(new("settlement", settlement.Id, settlement.ReceivedAt, settlement.ReceivedByUserId, null));
        events.AddRange(s.InitialLoads.Where(x => x.IsOutsideWorkingHours)
            .Select(x => new OutsideWorkingHoursEventDto("initial-load", x.Id, x.LoadedAt, x.RegisteredByUserId, null)));
        events.AddRange(s.Restocks.Where(x => x.IsOutsideWorkingHours)
            .Select(x => new OutsideWorkingHoursEventDto("restock", x.Id, x.OccurredAt, x.RegisteredByUserId, null)));
        events.AddRange(s.Deliveries.Where(x => x.IsOutsideWorkingHours)
            .Select(x => new OutsideWorkingHoursEventDto("visit", x.Id, x.DeliveredAt, null, s.DriverId)));
        events.AddRange(s.Promotions.Where(x => x.IsOutsideWorkingHours)
            .Select(x => new OutsideWorkingHoursEventDto("promotion", x.Id, x.RegisteredAt, null, x.DriverId)));
        events.AddRange(s.ClosedPromotions.Where(x => x.ClosedOutsideWorkingHours && x.ClosedAt.HasValue)
            .Select(x => new OutsideWorkingHoursEventDto("promotion-close", x.Id, x.ClosedAt!.Value, null, x.ClosedByDriverId)));
        return events.OrderBy(x => x.OccurredAt).ThenBy(x => x.Operation).ThenBy(x => x.Id).ToList();
    }
}
