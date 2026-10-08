using GMSoft.Domain.Enums;

namespace GMSoft.Application.Features.Sessions.DailySummary;

public record StockSummaryDto(int FullLoaded, int FullSold, int FullReturned,
    int FullDifference, int EmptyCollected, int EmptyReturned, int EmptyDifference)
{
    public static StockSummaryDto Sum(IEnumerable<StockSummaryDto> rows)
    {
        var list = rows.ToList();
        return new(list.Sum(x => x.FullLoaded), list.Sum(x => x.FullSold), list.Sum(x => x.FullReturned),
            list.Sum(x => x.FullDifference), list.Sum(x => x.EmptyCollected),
            list.Sum(x => x.EmptyReturned), list.Sum(x => x.EmptyDifference));
    }
}

public record ProductDailySummaryDto(Guid ProductId, string ProductDetail, StockSummaryDto Stock);
public record MoneyDailySummaryDto(decimal CashExpected, decimal Transfer, decimal Card,
    decimal? CashDeclared, decimal? CashDifference);
public record SessionDailySummaryDto(Guid SessionId, Guid VehicleId, string VehicleName,
    string VehicleLicensePlate, string DriverName, string ZoneName, DateTime OpenedAt,
    DateTime? ClosedAt, SessionStatus Status, bool IsClosed, int KilometersAtOpen,
    int? KilometersAtClose, DateTime? ReceivedAt, string? Notes, MoneyDailySummaryDto Money,
    IReadOnlyList<ProductDailySummaryDto> Products, StockSummaryDto Totals);
public record DayDailySummaryDto(bool IsClosed, int PendingSettlements, MoneyDailySummaryDto Money,
    IReadOnlyList<ProductDailySummaryDto> Products, StockSummaryDto Totals);
public record DailySummaryDto(Guid VehicleId, DateOnly Date,
    IReadOnlyList<SessionDailySummaryDto> Sessions, DayDailySummaryDto DayTotals);
