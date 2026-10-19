using FluentValidation;
using GMSoft.Application.Common;
using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.Finance;

// El repositorio devuelve grupos, no el detalle de cada visita.
public record SoldGroup(DateOnly Date, Guid VehicleId, decimal Sold);
public record UnitsGroup(DateOnly Date, Guid ProductId, Guid VehicleId, long Units, decimal Sold);
public record PaidGroup(DateOnly Date, Guid? DriverId, Guid? VehicleId, PaymentMethod Method, decimal Amount);
public record CostRate(Guid ProductId, DateOnly ValidFrom, decimal UnitCost);
public record CommissionRate(Guid DriverId, DateOnly ValidFrom, decimal Percent);
public record FinanceName(Guid Id, string Name);
public record ProfitData(IReadOnlyList<SoldGroup> Sales, IReadOnlyList<UnitsGroup> Units,
    IReadOnlyList<UnitsGroup> Promotions, IReadOnlyList<PaidGroup> Payments, IReadOnlyList<ExpenseDto> Expenses,
    IReadOnlyList<CostRate> Costs, IReadOnlyList<CommissionRate> Rates,
    IReadOnlyList<FinanceName> Products, IReadOnlyList<FinanceName> Drivers, IReadOnlyList<FinanceName> Vehicles);

public record GetMonthlyProfitQuery(int Year, int Month) : IRequest<MonthlyProfitDto>;
public class GetMonthlyProfitQueryValidator : AbstractValidator<GetMonthlyProfitQuery>
{
    public GetMonthlyProfitQueryValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2020, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}
public record ProfitTotals(decimal Sold, decimal Collected, decimal OnAccount, decimal CostOfSales,
    decimal PromotionCost, decimal Commissions, decimal Expenses, decimal Profit, decimal? MarginPercent);
public record ProductProfitDto(Guid ProductId, string ProductDetail, long Units, decimal Sold, decimal Cost,
    decimal Profit, decimal? MarginPercent, long UnitsWithoutCost, long PromotionUnits, decimal PromotionCost,
    long PromotionUnitsWithoutCost);
public record DriverProfitDto(Guid DriverId, string DriverName, decimal Collected, decimal EffectivePercent,
    decimal Commission, decimal CollectedWithoutRate);
public record VehicleProfitDto(Guid VehicleId, string VehicleName, decimal Sold, decimal CostOfSales,
    decimal PromotionCost, decimal Commissions, decimal Expenses, decimal Profit);
public record CategoryExpenseDto(ExpenseCategory Category, decimal Amount);
public record VehicleExpenseDto(Guid? VehicleId, string? VehicleName, decimal Amount);
public record ProfitWarning(string Code, string Message, Guid? ProductId = null, Guid? DriverId = null,
    long Units = 0, decimal Amount = 0);
public record PreviousProfitDto(int Year, int Month, decimal Sold, decimal Profit);
public record MonthlyProfitDto(int Year, int Month, ProfitTotals Totals,
    IReadOnlyDictionary<PaymentMethod, decimal> CollectedByMethod,
    IReadOnlyList<ProductProfitDto> Products, IReadOnlyList<DriverProfitDto> Drivers,
    IReadOnlyList<VehicleProfitDto> Vehicles, IReadOnlyList<CategoryExpenseDto> ExpensesByCategory,
    IReadOnlyList<VehicleExpenseDto> ExpensesByVehicle, PreviousProfitDto PreviousMonth,
    IReadOnlyList<ProfitWarning> Warnings);

public class MonthlyProfitHandler(IFinanceRepository repository, ICurrentUserService user, TimeProvider clock)
    : IRequestHandler<GetMonthlyProfitQuery, MonthlyProfitDto>
{
    public async Task<MonthlyProfitDto> Handle(GetMonthlyProfitQuery request, CancellationToken ct)
    {
        if (!user.IsInRole(AppRoles.Admin)) throw new ForbiddenException("Las ganancias las ve la oficina.");
        var first = new DateOnly(request.Year, request.Month, 1);
        var previous = first.AddMonths(-1);
        var range = BusinessTime.MonthRangeUtc(request.Year, request.Month);
        // Una lectura para ambos meses; la cantidad de consultas no depende de las ventas.
        var data = await repository.GetProfitDataAsync(BusinessTime.DayRangeUtc(previous).FromUtc, range.ToUtc, ct);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.Add(BusinessTime.Offset));
        var prior = Calculate(data, previous, today);
        var current = Calculate(data, first, today);
        return current with { PreviousMonth = new(previous.Year, previous.Month, prior.Totals.Sold, prior.Totals.Profit) };
    }

    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private static decimal? Margin(decimal profit, decimal sold) => sold == 0 ? null : Round(profit / sold * 100);
    private static MonthlyProfitDto Calculate(ProfitData data, DateOnly first, DateOnly today)
    {
        var end = first.AddMonths(1);
        bool InMonth(DateOnly date) => date >= first && date < end;
        var sales = data.Sales.Where(x => InMonth(x.Date)).ToArray();
        var costs = data.Costs.ToLookup(x => x.ProductId);
        var rates = data.Rates.ToLookup(x => x.DriverId);
        var names = data.Products.ToDictionary(x => x.Id, x => x.Name);
        var drivers = data.Drivers.ToDictionary(x => x.Id, x => x.Name);
        var vehicles = data.Vehicles.ToDictionary(x => x.Id, x => x.Name);
        var units = data.Units.Where(x => InMonth(x.Date)).Select(x => Price(x, false)).ToArray();
        var promos = data.Promotions.Where(x => InMonth(x.Date)).Select(x => Price(x, true)).ToArray();
        var allUnits = units.Concat(promos).ToArray();
        var paid = data.Payments.Where(x => InMonth(x.Date)).Select(x =>
        {
            var rate = x.DriverId is Guid id ? rates[id].Where(r => r.ValidFrom <= x.Date)
                .OrderByDescending(r => r.ValidFrom).FirstOrDefault() : null;
            return new { Row = x, Commission = x.Amount * (rate?.Percent ?? 0) / 100,
                Missing = x.DriverId.HasValue && rate is null };
        }).ToArray();
        var expenses = data.Expenses.Where(x => InMonth(x.Date)).ToArray();
        var productRows = allUnits.GroupBy(x => x.Row.ProductId).Select(g =>
        {
            var sold = Round(g.Sum(x => x.Row.Sold));
            var cost = Round(g.Where(x => !x.Promo).Sum(x => x.Cost));
            return new ProductProfitDto(g.Key, names.GetValueOrDefault(g.Key, ""), g.Where(x => !x.Promo).Sum(x => x.Row.Units),
                sold, cost, sold - cost, Margin(sold - cost, sold), g.Where(x => !x.Promo && x.Missing).Sum(x => x.Row.Units),
                g.Where(x => x.Promo).Sum(x => x.Row.Units), Round(g.Where(x => x.Promo).Sum(x => x.Cost)),
                g.Where(x => x.Promo && x.Missing).Sum(x => x.Row.Units));
        }).OrderBy(x => x.ProductDetail).ThenBy(x => x.ProductId).ToArray();
        var driverRows = paid.Where(x => x.Row.DriverId.HasValue).GroupBy(x => x.Row.DriverId!.Value).Select(g =>
        {
            var collected = Round(g.Sum(x => x.Row.Amount));
            var commission = Round(g.Sum(x => x.Commission));
            return new DriverProfitDto(g.Key, drivers.GetValueOrDefault(g.Key, ""), collected,
                collected == 0 ? 0 : Round(commission / collected * 100), commission,
                Round(g.Where(x => x.Missing).Sum(x => x.Row.Amount)));
        }).OrderBy(x => x.DriverName).ThenBy(x => x.DriverId).ToArray();
        var vehicleIds = sales.Select(x => x.VehicleId).Concat(allUnits.Select(x => x.Row.VehicleId))
            .Concat(paid.Where(x => x.Row.VehicleId.HasValue).Select(x => x.Row.VehicleId!.Value))
            .Concat(expenses.Where(x => x.VehicleId.HasValue).Select(x => x.VehicleId!.Value)).Distinct();
        var vehicleRows = vehicleIds.Select(id =>
        {
            var sold = Round(sales.Where(x => x.VehicleId == id).Sum(x => x.Sold));
            var cost = Round(units.Where(x => x.Row.VehicleId == id).Sum(x => x.Cost));
            var promo = Round(promos.Where(x => x.Row.VehicleId == id).Sum(x => x.Cost));
            var commission = Round(paid.Where(x => x.Row.VehicleId == id).Sum(x => x.Commission));
            var expense = Round(expenses.Where(x => x.VehicleId == id).Sum(x => x.Amount));
            return new VehicleProfitDto(id, vehicles.GetValueOrDefault(id, ""), sold, cost, promo, commission,
                expense, sold - cost - promo - commission - expense);
        }).OrderBy(x => x.VehicleName).ThenBy(x => x.VehicleId).ToArray();
        var totalSold = Round(sales.Sum(x => x.Sold));
        var totalPaid = Round(paid.Sum(x => x.Row.Amount));
        var totalCost = productRows.Sum(x => x.Cost);
        var totalPromo = productRows.Sum(x => x.PromotionCost);
        var totalCommission = driverRows.Sum(x => x.Commission);
        var totalExpense = Round(expenses.Sum(x => x.Amount));
        var profit = totalSold - totalCost - totalPromo - totalCommission - totalExpense;
        var warnings = new List<ProfitWarning>();
        warnings.AddRange(productRows.Where(x => x.UnitsWithoutCost + x.PromotionUnitsWithoutCost > 0)
            .Select(x => new ProfitWarning("MissingProductCost", "Hay unidades sin costo cargado.", x.ProductId,
                Units: x.UnitsWithoutCost + x.PromotionUnitsWithoutCost)));
        warnings.AddRange(driverRows.Where(x => x.CollectedWithoutRate > 0)
            .Select(x => new ProfitWarning("MissingCommission", "Hay cobros sin porcentaje cargado.",
                DriverId: x.DriverId, Amount: x.CollectedWithoutRate)));
        if (first > today) warnings.Add(new("FutureMonth", "El mes todavía no empezó."));
        else if (today < end) warnings.Add(new("CurrentMonth", "El mes está en curso; los números son parciales."));
        return new(first.Year, first.Month, new(totalSold, totalPaid, totalSold - totalPaid, totalCost, totalPromo,
            totalCommission, totalExpense, profit, Margin(profit, totalSold)),
            Enum.GetValues<PaymentMethod>().ToDictionary(m => m, m => Round(paid.Where(x => x.Row.Method == m).Sum(x => x.Row.Amount))),
            productRows, driverRows, vehicleRows,
            expenses.GroupBy(x => x.Category).OrderBy(g => g.Key).Select(g => new CategoryExpenseDto(g.Key, Round(g.Sum(x => x.Amount)))).ToArray(),
            expenses.GroupBy(x => x.VehicleId).OrderBy(g => g.Key).Select(g => new VehicleExpenseDto(g.Key,
                g.Key is Guid id ? vehicles.GetValueOrDefault(id) : null, Round(g.Sum(x => x.Amount)))).ToArray(),
            new(first.AddMonths(-1).Year, first.AddMonths(-1).Month, 0, 0), warnings);

        (UnitsGroup Row, bool Promo, decimal Cost, bool Missing) Price(UnitsGroup row, bool promo)
        {
            var cost = costs[row.ProductId].Where(c => c.ValidFrom <= row.Date).OrderByDescending(c => c.ValidFrom).FirstOrDefault();
            return (row, promo, row.Units * (cost?.UnitCost ?? 0), cost is null);
        }
    }
}
