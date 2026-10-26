using GMSoft.Domain.Enums;

namespace GMSoft.Application.Features.Sessions.DailySummary;

public record StockSummaryDto(int FullLoaded, int FullSold, int FullReturned,
    int FullDifference, int EmptyCollected, int EmptyReturned, int EmptyDifference)
{
    /// <summary>De los vacíos contabilizados, los que los clientes tenían sin registrar.</summary>
    public int EmptyRecovered { get; init; }

    public static StockSummaryDto Sum(IEnumerable<StockSummaryDto> rows)
    {
        var list = rows.ToList();
        return new(list.Sum(x => x.FullLoaded), list.Sum(x => x.FullSold), list.Sum(x => x.FullReturned),
            list.Sum(x => x.FullDifference), list.Sum(x => x.EmptyCollected),
            list.Sum(x => x.EmptyReturned), list.Sum(x => x.EmptyDifference)) { EmptyRecovered = list.Sum(x => x.EmptyRecovered) };
    }
}

public record ProductDailySummaryDto(Guid ProductId, string ProductDetail, StockSummaryDto Stock);
public record MoneyDailySummaryDto(decimal CashExpected, decimal Transfer, decimal Card,
    decimal? CashDeclared, decimal? CashDifference);
public record SessionDailySummaryDto(Guid SessionId, Guid VehicleId, string VehicleName,
    string VehicleLicensePlate, string DriverName, string ZoneName, DateTime OpenedAt,
    DateTime? ClosedAt, SessionStatus Status, bool IsClosed, int KilometersAtOpen,
    int? KilometersAtClose, DateTime? ReceivedAt, string? Notes, MoneyDailySummaryDto Money,
    IReadOnlyList<ProductDailySummaryDto> Products, StockSummaryDto Totals)
{
    public int OutOfRouteCount { get; init; }
    public bool OpenedOutsideWorkingHours { get; init; }
    public bool ClosedOutsideWorkingHours { get; init; }
    public bool SettlementOutsideWorkingHours { get; init; }
    public Guid? OpenedByUserId { get; init; }
    public Guid? ClosedByUserId { get; init; }
    public IReadOnlyList<GMSoft.Application.Features.WorkingHours.OutsideWorkingHoursEventDto> OutsideWorkingHoursEvents { get; init; } = [];
}
public record DayDailySummaryDto(bool IsClosed, int PendingSettlements, MoneyDailySummaryDto Money,
    IReadOnlyList<ProductDailySummaryDto> Products, StockSummaryDto Totals);
public record DailySummaryDto(Guid VehicleId, DateOnly Date,
    IReadOnlyList<SessionDailySummaryDto> Sessions, DayDailySummaryDto DayTotals);
