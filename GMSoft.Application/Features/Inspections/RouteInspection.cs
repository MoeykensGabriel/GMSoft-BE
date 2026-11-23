using FluentValidation;
using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Customers.Common;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.Inspections;

// El controlador se sube a un camión que ya salió y recorre sus mismos clientes. Cuenta
// los envases (y los corrige en el momento) y confirma si el cliente sigue comprando.
// Nada de esto muestra plata: ni precios, ni deuda, ni lo cobrado.

public record InspectionDepartureDto(Guid SessionId, string VehicleName, string LicensePlate,
    string DriverName, DateTime OpenedAt, IReadOnlyList<string> Zones);

/// <summary>Lo que marcó el chofer en esta salida: tipo de visita, sin montos.</summary>
public record DriverVisitDto(DeliveryType Type, NoPurchaseReason? NoPurchaseReason, DateTime At);

public record InspectionContainerDto(Guid ProductId, string Product, int Quantity);

public record InspectionCountDto(Guid ProductId, int SystemQuantity, int RealQuantity);
public record InspectionDto(bool StillBuys, NotBuyingReason? NotBuyingReason, string? Notes, DateTime At,
    IReadOnlyList<InspectionCountDto> Containers);

public record InspectionCustomerDto(Guid Id, string DisplayName, string Address, string? Notes, string ZoneName,
    int RouteOrder, DateTime? LastPurchaseAt, int WeeksWithoutPurchase, string ActivityStatus,
    DateTime? PurchaseConfirmedAt, DriverVisitDto? DriverVisit, IReadOnlyList<InspectionContainerDto> Containers,
    InspectionDto? Inspection);

public record InspectionRouteDto(InspectionDepartureDto Departure, IReadOnlyList<InspectionCustomerDto> Customers);

public record GetInspectionDeparturesQuery : IRequest<IReadOnlyList<InspectionDepartureDto>>;
public record GetInspectionRouteQuery(Guid SessionId) : IRequest<InspectionRouteDto>;

public record InspectionCountInput(Guid ProductId, int RealQuantity);
public record SaveCustomerInspectionCommand(Guid SessionId, Guid CustomerId, InspectionCountInput[] Containers,
    bool StillBuys, NotBuyingReason? NotBuyingReason, string? Notes) : IRequest<InspectionCustomerDto>;

public record BalanceProduct(Guid Id, string Detail);
public record CustomerVisitRow(Guid CustomerId, DeliveryType Type, NoPurchaseReason? NoPurchaseReason, DateTime At);

public interface IInspectionRepository
{
    Task<IReadOnlyList<InspectionDepartureDto>> GetOpenDeparturesAsync(Guid? sessionId, CancellationToken ct);
    /// <summary>Productos que se siguen por saldo: los únicos que el controlador cuenta.</summary>
    Task<IReadOnlyList<BalanceProduct>> GetBalanceProductsAsync(CancellationToken ct);
    Task<IReadOnlyDictionary<(Guid CustomerId, Guid ProductId), int>> GetBalancesAsync(
        IReadOnlyCollection<Guid> customerIds, CancellationToken ct);
    /// <summary>La última visita del chofer a cada cliente en la salida.</summary>
    Task<IReadOnlyDictionary<Guid, CustomerVisitRow>> GetDriverVisitsAsync(Guid sessionId, CancellationToken ct);
    Task<IReadOnlyList<CustomerInspection>> GetBySessionAsync(Guid sessionId, CancellationToken ct);
    /// <summary>Con tracking: se actualiza si ya se había controlado.</summary>
    Task<CustomerInspection?> GetAsync(Guid sessionId, Guid customerId, CancellationToken ct);
    Task AddAsync(CustomerInspection inspection, CancellationToken ct);
    /// <summary>Solo esa columna: no pisa una edición del cliente que llegue al mismo tiempo.</summary>
    Task ConfirmPurchaseAsync(Guid customerId, DateTime at, CancellationToken ct);
}

public class SaveCustomerInspectionValidator : AbstractValidator<SaveCustomerInspectionCommand>
{
    public SaveCustomerInspectionValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Containers).NotNull()
            .Must(c => c is null || c.Select(i => i.ProductId).Distinct().Count() == c.Length)
            .WithMessage("Cada producto va una sola vez.");
        RuleForEach(x => x.Containers).ChildRules(c =>
        {
            c.RuleFor(i => i.ProductId).NotEmpty();
            c.RuleFor(i => i.RealQuantity).InclusiveBetween(0, 999);
        });
        RuleFor(x => x.NotBuyingReason).NotNull().When(x => !x.StillBuys)
            .WithMessage("Elegí por qué no compra más.");
        RuleFor(x => x.NotBuyingReason).IsInEnum();
        RuleFor(x => x.Notes).NotEmpty().When(x => !x.StillBuys && x.NotBuyingReason == NotBuyingReason.Other)
            .WithMessage("Con «Otro», contá en la nota por qué no compra.");
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public class RouteInspectionHandler(IInspectionRepository inspections, ISessionRepository sessions,
    ICustomerRepository customers, IDriverRepository drivers, IContainerBalanceRepository balances,
    IRepository<ContainerMovement> movements, ICurrentUserService user, IUnitOfWork uow,
    CustomerActivityPolicy policy, TimeProvider clock) :
    IRequestHandler<GetInspectionDeparturesQuery, IReadOnlyList<InspectionDepartureDto>>,
    IRequestHandler<GetInspectionRouteQuery, InspectionRouteDto>,
    IRequestHandler<SaveCustomerInspectionCommand, InspectionCustomerDto>
{
    public async Task<IReadOnlyList<InspectionDepartureDto>> Handle(GetInspectionDeparturesQuery request, CancellationToken ct)
    {
        await InspectorAsync(ct);
        return await inspections.GetOpenDeparturesAsync(null, ct);
    }

    public async Task<InspectionRouteDto> Handle(GetInspectionRouteQuery request, CancellationToken ct)
    {
        await InspectorAsync(ct);
        var (session, zones, departure) = await OpenSessionAsync(request.SessionId, ct);
        var route = await RouteAsync(session, zones, ct);
        return new(departure, await ToDtosAsync(session.Id, route, ct));
    }

    public async Task<InspectionCustomerDto> Handle(SaveCustomerInspectionCommand request, CancellationToken ct)
    {
        var inspector = await InspectorAsync(ct);
        var (session, zones, _) = await OpenSessionAsync(request.SessionId, ct);
        var customer = await customers.GetWithZoneAsync(request.CustomerId, ct)
            ?? throw new NotFoundException(nameof(Customer), request.CustomerId);
        if (!customer.IsActive || customer.VehicleId != session.VehicleId || !zones.Contains(customer.ZoneId)
            || customer.VisitDays is null || !customer.VisitDays.Intersect(CustomerRouteAccess.Days(session)).Any())
            throw new BadRequestException("El cliente no es del recorrido de este camión.");

        var products = (await inspections.GetBalanceProductsAsync(ct)).ToDictionary(p => p.Id);
        if (request.Containers.Any(c => !products.ContainsKey(c.ProductId)))
            throw new BadRequestException("Solo se cuentan los envases que se siguen por saldo.");

        var now = clock.GetUtcNow().UtcDateTime;
        var nombre = $"{inspector.FirstName} {inspector.LastName}".Trim();
        await uow.ExecuteInTransactionAsync(async () =>
        {
            var counts = new List<InspectionContainerCount>();
            foreach (var item in request.Containers)
            {
                // Mismo criterio que el ajuste de la oficina: saldo bloqueado, se guarda la diferencia.
                var anterior = await balances.LockQuantityAsync(customer.Id, item.ProductId, ct);
                counts.Add(new() { ProductId = item.ProductId, SystemQuantity = anterior, RealQuantity = item.RealQuantity });
                var delta = item.RealQuantity - anterior;
                if (delta == 0) continue;
                await movements.AddAsync(new ContainerMovement
                {
                    ProductId = item.ProductId, CustomerId = customer.Id, Quantity = delta,
                    Type = ContainerMovementType.Adjustment, OccurredAt = now,
                    RegisteredByUserId = user.UserId, Notes = $"Control en ruta: {nombre}",
                }, ct);
                await balances.AdjustAsync(customer.Id, item.ProductId, delta, ct);
            }

            var inspection = await inspections.GetAsync(session.Id, customer.Id, ct);
            if (inspection is null)
            {
                inspection = new CustomerInspection { DeliverySessionId = session.Id, CustomerId = customer.Id };
                await inspections.AddAsync(inspection, ct);
            }
            inspection.InspectorId = inspector.Id;
            inspection.InspectedByUserId = user.UserId ?? Guid.Empty;
            inspection.InspectedAt = now;
            inspection.StillBuys = request.StillBuys;
            inspection.NotBuyingReason = request.StillBuys ? null : request.NotBuyingReason;
            inspection.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
            // Se actualiza en el lugar: reemplazar la lista choca con las filas que EF ya sigue.
            inspection.Containers.RemoveAll(c => counts.All(n => n.ProductId != c.ProductId));
            foreach (var count in counts)
            {
                var existing = inspection.Containers.FirstOrDefault(c => c.ProductId == count.ProductId);
                if (existing is null) inspection.Containers.Add(count);
                else { existing.SystemQuantity = count.SystemQuantity; existing.RealQuantity = count.RealQuantity; }
            }

            if (request.StillBuys) await inspections.ConfirmPurchaseAsync(customer.Id, now, ct);
            await uow.SaveChangesAsync(ct);
        }, ct);

        if (request.StillBuys) customer.PurchaseConfirmedAt = now;
        return (await ToDtosAsync(session.Id, [customer], ct))[0];
    }

    private async Task<Driver> InspectorAsync(CancellationToken ct)
    {
        if (!user.IsInRole(AppRoles.Inspector) || user.DriverId is not Guid id)
            throw new ForbiddenException("Solo un controlador puede hacer controles en ruta.");
        var inspector = await drivers.GetByIdAsync(id, ct) ?? throw new ForbiddenException("No se encontró tu ficha.");
        if (!inspector.IsInspector || !inspector.IsActive)
            throw new ForbiddenException("Tu usuario no está habilitado como controlador.");
        return inspector;
    }

    private async Task<(DeliverySession Session, IReadOnlyList<Guid> Zones, InspectionDepartureDto Departure)> OpenSessionAsync(
        Guid id, CancellationToken ct)
    {
        var session = await sessions.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(DeliverySession), id);
        var departure = (await inspections.GetOpenDeparturesAsync(id, ct)).FirstOrDefault();
        if (session.Status != SessionStatus.Open || departure is null)
            throw new ConflictException("Ese camión ya volvió: elegí otro.");
        var zones = await sessions.GetZoneIdsAsync(session.Id, ct);
        return (session, zones.Count > 0 ? zones : [session.ZoneId], departure);
    }

    /// <summary>El mismo recorrido que ve el chofer: sus zonas en orden y, dentro, por RouteOrder.</summary>
    private async Task<List<Customer>> RouteAsync(DeliverySession session, IReadOnlyList<Guid> zones, CancellationToken ct)
    {
        var route = new List<Customer>();
        foreach (var zoneId in zones)
            route.AddRange((await customers.GetPagedAsync(1, int.MaxValue, null, zoneId, true, null,
                ct, CustomerRouteAccess.Days(session), session.VehicleId)).Items);
        return route;
    }

    private async Task<List<InspectionCustomerDto>> ToDtosAsync(Guid sessionId, IReadOnlyList<Customer> route, CancellationToken ct)
    {
        var ids = route.Select(c => c.Id).ToList();
        var purchases = await customers.GetLastPurchaseDatesAsync(ids, ct);
        var weeks = await CustomerActivityReader.MissedWeeksAsync(customers, route, purchases, ct);
        var products = await inspections.GetBalanceProductsAsync(ct);
        var saldos = await inspections.GetBalancesAsync(ids, ct);
        var visits = await inspections.GetDriverVisitsAsync(sessionId, ct);
        var done = (await inspections.GetBySessionAsync(sessionId, ct)).ToDictionary(i => i.CustomerId);

        return route.Select(c =>
        {
            var visit = visits.TryGetValue(c.Id, out var v) ? new DriverVisitDto(v.Type, v.NoPurchaseReason, v.At) : null;
            var inspection = done.TryGetValue(c.Id, out var i)
                ? new InspectionDto(i.StillBuys, i.NotBuyingReason, i.Notes, i.InspectedAt,
                    i.Containers.Select(x => new InspectionCountDto(x.ProductId, x.SystemQuantity, x.RealQuantity)).ToList())
                : null;
            return new InspectionCustomerDto(c.Id,
                string.IsNullOrWhiteSpace(c.BusinessName) ? c.ContactName : c.BusinessName, c.Address, c.Notes,
                c.Zone?.Name ?? string.Empty, c.RouteOrder,
                purchases.TryGetValue(c.Id, out var compra) ? compra : null, weeks[c.Id], policy.GetStatus(weeks[c.Id]),
                c.PurchaseConfirmedAt, visit,
                products.Select(p => new InspectionContainerDto(p.Id, p.Detail,
                    saldos.TryGetValue((c.Id, p.Id), out var q) ? q : 0)).ToList(),
                inspection);
        }).ToList();
    }
}
