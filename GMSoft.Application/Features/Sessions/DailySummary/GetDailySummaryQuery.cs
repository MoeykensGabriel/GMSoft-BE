using FluentValidation;
using GMSoft.Application.Common;
using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.Sessions.DailySummary;

public record GetDailySummaryQuery(Guid VehicleId, DateOnly Date) : IRequest<DailySummaryDto>;

public class GetDailySummaryQueryValidator : AbstractValidator<GetDailySummaryQuery>
{
    public GetDailySummaryQueryValidator()
    {
        RuleFor(x => x.VehicleId).NotEmpty();
        RuleFor(x => x.Date).NotEmpty().LessThan(DateOnly.MaxValue);
    }
}

public class GetDailySummaryQueryHandler(IDailySummaryRepository repository, ICurrentUserService user)
    : IRequestHandler<GetDailySummaryQuery, DailySummaryDto>
{
    public async Task<DailySummaryDto> Handle(GetDailySummaryQuery request, CancellationToken cancellationToken)
    {
        if (!user.IsInRole(AppRoles.Admin)) throw new ForbiddenException("El resumen diario es solo para ADMIN.");
        var (from, to) = BusinessTime.DayRangeUtc(request.Date);
        var data = await repository.GetAsync(request.VehicleId, from, to, cancellationToken);
        var sessions = data.Sessions.OrderBy(s => s.OpenedAt).ThenBy(s => s.Id).Select(s =>
        {
            var products = s.StockMovements.GroupBy(m => m.ProductId).Select(g =>
            {
                int Total(ContainerState state, SessionStockMovementType type) =>
                    g.Where(m => m.State == state && m.Type == type).Sum(m => m.Quantity);
                var fullSold = -Total(ContainerState.Full, SessionStockMovementType.Delivered);
                var fullReturned = -Total(ContainerState.Full, SessionStockMovementType.ReturnedAtClose);
                var emptyReturned = -Total(ContainerState.Empty, SessionStockMovementType.ReturnedAtClose);
                // Las entradas netas incluyen ajustes de ambos signos y traspasos. No
                // convertir un ajuste negativo en una venta ni perderlo del cuadre.
                var fullLoaded = g.Where(m => m.State == ContainerState.Full &&
                    m.Type != SessionStockMovementType.Delivered && m.Type != SessionStockMovementType.ReturnedAtClose)
                    .Sum(m => m.Quantity);
                var emptyCollected = g.Where(m => m.State == ContainerState.Empty &&
                    m.Type != SessionStockMovementType.ReturnedAtClose).Sum(m => m.Quantity);
                return new ProductDailySummaryDto(g.Key, g.First().Product.Detail,
                    new(fullLoaded, fullSold, fullReturned, fullLoaded - fullSold - fullReturned,
                        emptyCollected, emptyReturned, emptyCollected - emptyReturned));
            }).OrderBy(p => p.ProductDetail).ThenBy(p => p.ProductId).ToList();
            decimal Paid(PaymentMethod method) => data.Payments
                .Where(p => p.SessionId == s.Id && p.Method == method).Sum(p => p.Amount);
            var cash = Paid(PaymentMethod.Cash);
            var declared = s.CashSettlement?.AmountReceived;
            return new SessionDailySummaryDto(s.Id, s.VehicleId, s.Vehicle.Name, s.Vehicle.LicensePlate,
                $"{s.Driver.FirstName} {s.Driver.LastName}".Trim(), s.Zone.Name, s.OpenedAt, s.ClosedAt,
                s.Status, s.Status == SessionStatus.Closed, s.KilometersAtOpen, s.KilometersAtClose,
                s.CashSettlement?.ReceivedAt, s.CashSettlement?.Notes,
                new(cash, Paid(PaymentMethod.Transfer), Paid(PaymentMethod.Card), declared, cash - declared),
                products, StockSummaryDto.Sum(products.Select(p => p.Stock)));
        }).ToList();

        var dayProducts = sessions.SelectMany(s => s.Products).GroupBy(p => p.ProductId)
            .Select(g => new ProductDailySummaryDto(g.Key, g.First().ProductDetail,
                StockSummaryDto.Sum(g.Select(p => p.Stock))))
            .OrderBy(p => p.ProductDetail).ThenBy(p => p.ProductId).ToList();
        var pending = sessions.Count(s => s.Money.CashDeclared is null);
        // Una rendicion incompleta no equivale a cero ni a un faltante confirmado.
        decimal? dayDeclared = pending == 0 ? sessions.Sum(s => s.Money.CashDeclared!.Value) : null;
        var dayCash = sessions.Sum(s => s.Money.CashExpected);
        return new(request.VehicleId, request.Date, sessions,
            new(sessions.All(s => s.IsClosed), pending,
                new(dayCash, sessions.Sum(s => s.Money.Transfer), sessions.Sum(s => s.Money.Card),
                    dayDeclared, dayCash - dayDeclared), dayProducts,
                StockSummaryDto.Sum(dayProducts.Select(p => p.Stock))));
    }
}
