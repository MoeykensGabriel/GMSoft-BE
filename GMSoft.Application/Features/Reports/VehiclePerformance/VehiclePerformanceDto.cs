namespace GMSoft.Application.Features.Reports.VehiclePerformance;

public record VehiclePerformanceDto(DateOnly From, DateOnly To, IReadOnlyList<VehiclePerformanceLineDto> Vehicles);

public record VehiclePerformanceLineDto(
    Guid VehicleId, string VehicleName, string LicensePlate, IReadOnlyList<string> DriverNames,
    int Sessions, int ClosedSessions, decimal Sold, decimal Collected,
    CollectedByMethodDto CollectedByMethod, int RouteCustomers, int VisitedCustomers,
    int BuyingCustomers, int Visits, int Sales, NoPurchaseDto NoPurchase,
    IReadOnlyList<ProductPerformanceDto> UnitsByProduct, ShortagesDto Shortages,
    decimal CashDifference, long Kilometers)
{
    // Same net new debt criterion as the session settlement; payments have no sale FK.
    public decimal OnAccount => Sold - Collected;
    public decimal? CollectedPercent => Sold == 0 ? null : Collected / Sold;
    public decimal? BuyingPercent => VisitedCustomers == 0 ? null : (decimal)BuyingCustomers / VisitedCustomers;
    public decimal? AverageTicket => Sales == 0 ? null : Sold / Sales;
    public decimal? SoldPerKilometer => Kilometers == 0 ? null : Sold / Kilometers;
}

public record CollectedByMethodDto(decimal Cash, decimal Transfer, decimal Card);
public record NoPurchaseDto(int Total, IReadOnlyDictionary<string, int> ByReason);
public record ProductPerformanceDto(Guid ProductId, string ProductDetail, long Units, decimal Amount);
public record ShortagesDto(long Full, long Empty);
