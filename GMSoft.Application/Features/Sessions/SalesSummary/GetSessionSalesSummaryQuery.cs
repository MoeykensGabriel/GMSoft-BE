using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.Sessions.SalesSummary;

/// <summary>
/// Lo que el chofer lleva vendido en su salida: la plata por medio de pago y las
/// unidades por producto. Las promociones no cuentan: no son ventas.
/// </summary>
public record GetSessionSalesSummaryQuery(Guid Id) : IRequest<SessionSalesSummaryDto>;

/// <param name="Total">Lo vendido, cobrado o no.</param>
/// <param name="Cash">El efectivo cobrado: lo que tiene que rendir.</param>
public record SessionSalesSummaryDto(
    decimal Total,
    decimal Cash,
    decimal Transfer,
    decimal Card,
    int     Sales,
    int     Visits,
    IReadOnlyList<SoldProductDto> Products);

public record SoldProductDto(Guid ProductId, string ProductDetail, int Quantity);

public class GetSessionSalesSummaryQueryHandler(ISessionRepository sessions, ICurrentUserService currentUser)
    : IRequestHandler<GetSessionSalesSummaryQuery, SessionSalesSummaryDto>
{
    public async Task<SessionSalesSummaryDto> Handle(GetSessionSalesSummaryQuery request, CancellationToken ct)
    {
        var session = await sessions.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException(nameof(DeliverySession), request.Id);
        // Mismo criterio que las visitas: el chofer ve las suyas, el admin todas.
        if (!currentUser.IsInRole(AppRoles.Admin) && currentUser.DriverId != session.DriverId)
            throw new ForbiddenException("Esta salida es de otro chofer.");

        var visitas = await sessions.GetDeliveriesAsync(session.Id, ct);
        var ventas = visitas.Where(v => v.Type == DeliveryType.Sale).ToList();
        var pagos = await sessions.GetPaymentsByCustomerAsync(session.Id, ct);
        decimal Cobrado(PaymentMethod method) => pagos.Where(p => p.Method == method).Sum(p => p.Amount);

        var productos = ventas.SelectMany(v => v.Items)
            .GroupBy(i => new { i.ProductId, i.ProductDetail })
            .Select(g => new SoldProductDto(g.Key.ProductId, g.Key.ProductDetail, g.Sum(i => i.Quantity)))
            .Where(p => p.Quantity > 0)
            .OrderBy(p => p.ProductDetail).ToList();

        return new SessionSalesSummaryDto(ventas.Sum(v => v.Total), Cobrado(PaymentMethod.Cash),
            Cobrado(PaymentMethod.Transfer), Cobrado(PaymentMethod.Card), ventas.Count, visitas.Count, productos);
    }
}
