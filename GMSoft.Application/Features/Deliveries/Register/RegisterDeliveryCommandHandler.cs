using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using GMSoft.Application.Features.Customers.Common;
using GMSoft.Domain.Enums;
using MediatR;
using GMSoft.Application.Features.WorkingHours;

namespace GMSoft.Application.Features.Deliveries.Register;

public class RegisterDeliveryCommandHandler
    : IRequestHandler<RegisterDeliveryCommand, RegisterDeliveryResult>
{
    private readonly ISessionRepository _sessions;
    private readonly ICustomerRepository _customers;
    private readonly IProductRepository _products;
    private readonly ICustomerPriceRepository _prices;
    private readonly IContainerBalanceRepository _balances;
    private readonly IRepository<Payment> _payments;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly WorkingHoursService _workingHours;

    public RegisterDeliveryCommandHandler(
        ISessionRepository sessions,
        ICustomerRepository customers,
        IProductRepository products,
        ICustomerPriceRepository prices,
        IContainerBalanceRepository balances,
        IRepository<Payment> payments,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        WorkingHoursService workingHours)
    {
        _sessions    = sessions;
        _customers   = customers;
        _products    = products;
        _prices      = prices;
        _balances    = balances;
        _payments    = payments;
        _currentUser = currentUser;
        _unitOfWork  = unitOfWork;
        _workingHours = workingHours;
    }

    public async Task<RegisterDeliveryResult> Handle(
        RegisterDeliveryCommand request,
        CancellationToken cancellationToken)
    {
        var driverId = _currentUser.DriverId
            ?? throw new ForbiddenException("Solo un chofer registra visitas.");

        // Un reintento pertenece al chofer original, aunque la salida ya este recibida.
        if (request.ClientRequestId is Guid pedido &&
            await _sessions.GetDeliveryByClientRequestAsync(pedido, cancellationToken) is { } previa)
        {
            var originalSession = await _sessions.GetByIdAsync(previa.DeliverySessionId, cancellationToken);
            if (originalSession?.DriverId != driverId)
                throw new ForbiddenException("El envio pertenece a otro chofer.");
            return new RegisterDeliveryResult(previa.Id, previa.CustomerId, previa.Total,
                await _customers.GetAccountBalanceAsync(previa.CustomerId, cancellationToken));
        }

        var session = await _sessions.GetOpenByDriverAsync(driverId, cancellationToken)
            ?? throw new ConflictException(
                "No tenes una sesion de reparto abierta. Abri una antes de registrar visitas.");
        // Después de buscar la clave: si la visita ya había llegado, el reintento la devuelve igual.
        if (request.ExpectedSessionId is Guid esperada && esperada != session.Id)
            throw new ConflictException(
                "La salida en que se cargó esta visita ya se cerró: no se registra en la salida actual.");

        var productos = await ResolverProductosAsync(request, cancellationToken);
        request = CompletarEnvasesEntregados(request, productos);
        ValidarEnvases(request, productos);

        foreach (var loan in request.LoansOut.Concat(request.LoansIn))
            if (!productos[loan.ProductId].CanBeLent || productos[loan.ProductId].Tracking != ContainerTracking.None)
                throw new BadRequestException("Solo se pueden prestar o retirar productos habilitados sin envase.");

        var ahora   = default(DateTime);
        var usuario = _currentUser.UserId;

        var customerId = Guid.Empty;
        var deliveryId = Guid.Empty;
        var total      = 0m;

        // Todo junto o nada. Si se guardara la venta y fallaran los envases, el libro
        // mayor dejaria de explicar el saldo del cliente, que es justo lo que este
        // sistema existe para evitar.
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // Una visita a la vez por salida. Sin este bloqueo dos ventas simultaneas
            // leen el mismo stock y juntas venden mas de lo que hay a bordo.
            if (!await _sessions.LockOpenAsync(session.Id, cancellationToken))
                throw new ConflictException(
                    "La salida ya fue recibida. No se pueden registrar mas visitas en ella.");

            // El mismo envio pudo entrar mientras esperabamos el bloqueo (doble toque,
            // reintento sin señal): se devuelve esa visita en vez de chocar con el
            // indice unico.
            if (request.ClientRequestId is Guid repetido &&
                await _sessions.GetDeliveryByClientRequestAsync(repetido, cancellationToken) is { } ganadora)
            {
                if (ganadora.DeliverySessionId != session.Id)
                    throw new ConflictException("El identificador ya pertenece a otra salida.");
                deliveryId = ganadora.Id;
                customerId = ganadora.CustomerId;
                total      = ganadora.Total;
                return;
            }

            var horario = await _workingHours.EvaluateAsync(cancellationToken);
            ahora = horario.UtcNow;
            await DeliveryStockRules.EnsureAvailableAsync(_sessions, session.Id, request.Items.Concat(request.LoansOut.Select(l => new DeliveryItemLine(l.ProductId, l.Quantity)))
                .GroupBy(l => l.ProductId).Select(g => new DeliveryItemLine(g.Key, g.Sum(l => l.Quantity))).ToList(),
                productos, cancellationToken);

            var customer = await ResolverClienteAsync(request, session, cancellationToken);
            customerId = customer.Id;

            if (request.Type == DeliveryType.NoPurchase)
            {
                var visits = await _sessions.GetCustomerVisitsAsync(session.Id, customer.Id, cancellationToken);
                if (visits.Any(d => d.Type == DeliveryType.Sale))
                    throw new ConflictException("Ya compró en esta salida.");
                var visit = visits.SingleOrDefault(d => d.Type == DeliveryType.NoPurchase);
                if (visit is null)
                {
                    visit = new Delivery { DeliverySessionId = session.Id, CustomerId = customer.Id,
                        Type = DeliveryType.NoPurchase, ClientRequestId = request.ClientRequestId };
                    session.Deliveries.Add(visit);
                }
                visit.NoPurchaseReason = request.NoPurchaseReason;
                visit.Notes = request.Notes?.Trim();
                visit.DeliveredAt = ahora;
                visit.IsOutsideWorkingHours = horario.IsOutsideWorkingHours;
                visit.NoPurchaseRequestIds = [.. visit.NoPurchaseRequestIds ?? [], request.ClientRequestId!.Value];
                _customers.UpdateLastVisit(customer, ahora);
                _sessions.Update(session);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                deliveryId = visit.Id;
                return;
            }

            // Bloqueamos todos los saldos en el mismo orden antes de calcular los excesos.
            var balances = new Dictionary<Guid, int>();
            foreach (var id in request.ContainersIn.Concat(request.ContainersOut).Concat(request.LoansIn)
                .Concat(request.LoansOut).Select(l => l.ProductId).Distinct().OrderBy(id => id))
                balances[id] = await _balances.LockQuantityAsync(customer.Id, id, cancellationToken);

            foreach (var returned in request.LoansIn.OrderBy(l => l.ProductId))
                if (returned.Quantity > balances[returned.ProductId])
                    throw new ConflictException("El cliente no puede devolver más de lo que tiene prestado.");

            var exchanges = new List<(Guid ProductId, int Quantity, string Notes)>();
            var used = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            foreach (var returned in request.ContainersIn.OrderBy(l => l.ProductId))
            {
                var delivered = request.ContainersOut.FirstOrDefault(c => c.ProductId == returned.ProductId)?.Quantity ?? 0;
                var excess = returned.Quantity - (long)balances[returned.ProductId] - delivered;
                if (excess <= 0) continue;
                var group = productos[returned.ProductId].ExchangeGroup;
                var candidates = request.ContainersOut.Where(l => l.ProductId != returned.ProductId &&
                    !string.IsNullOrWhiteSpace(group) && string.Equals(productos[l.ProductId].ExchangeGroup,
                        group, StringComparison.OrdinalIgnoreCase)).OrderByDescending(l => l.Quantity).ThenBy(l => l.ProductId).ToList();
                var total = request.ContainersOut.Where(l => group != null && string.Equals(
                    productos[l.ProductId].ExchangeGroup, group, StringComparison.OrdinalIgnoreCase)).Sum(l => (long)l.Quantity);
                // Lo que cubre un cambio dentro del grupo (vendió Bidón 12 y le devolvieron uno de 20).
                var cambio = request.Type == DeliveryType.Sale && group != null && candidates.Count > 0
                    ? Math.Min(excess, Math.Min(candidates.Sum(l => (long)l.Quantity), total - used.GetValueOrDefault(group)))
                    : 0;
                if (cambio > 0) used[group!] = used.GetValueOrDefault(group!) + cambio;
                // El resto lo tenía sin registrar (por ejemplo, del sistema anterior). Si el chofer lo
                // trae es porque lo tenía: se recibe igual, el vacío sube al camión y el saldo queda en 0.
                // Va en una sola anotación por producto: la base admite una por tipo en cada visita.
                var sinRegistrar = excess - cambio;
                var nota = cambio == 0 ? "Tenía sin registrar"
                    : sinRegistrar == 0 ? $"Cambio por {productos[candidates[0].ProductId].Detail}"
                    : $"Cambio por {productos[candidates[0].ProductId].Detail}; {sinRegistrar} tenía sin registrar";
                exchanges.Add((returned.ProductId, (int)excess, nota));
            }

            var delivery = new Delivery
            {
                DeliverySessionId = session.Id,
                CustomerId        = customer.Id,
                Type              = request.Type,
                DeliveredAt       = ahora,
                IsOutsideWorkingHours = horario.IsOutsideWorkingHours,
                Notes             = request.Notes?.Trim(),
                ClientRequestId   = request.ClientRequestId
            };

            total = await AgregarVentaAsync(request, delivery, session, customer.Id, ahora, usuario, cancellationToken);
            delivery.Total = total;

            await AgregarEnvasesAsync(request, delivery, session, customer.Id, ahora, usuario, exchanges, cancellationToken);

            _customers.UpdateLastVisit(customer, ahora);
            session.Deliveries.Add(delivery);
            _sessions.Update(session);

            // Sin importe se cobra la venta de esta visita, con el total calculado
            // aca. No toca deuda anterior. Si la venta dio cero no hay nada que cobrar.
            var cobrado = request.Payment?.Amount ?? total;

            if (request.Payment is not null && cobrado > 0)
            {
                await _payments.AddAsync(new Payment
                {
                    CustomerId        = customer.Id,
                    DeliverySessionId = session.Id,
                    Amount            = cobrado,
                    Method            = request.Payment.Method,
                    PaidAt            = ahora
                }, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            deliveryId = delivery.Id;
        }, cancellationToken);

        var saldo = await _customers.GetAccountBalanceAsync(customerId, cancellationToken);

        return new RegisterDeliveryResult(deliveryId, customerId, total, saldo);
    }

    private static RegisterDeliveryCommand CompletarEnvasesEntregados(
        RegisterDeliveryCommand request, IReadOnlyDictionary<Guid, Product> products)
    {
        if (request.Type != DeliveryType.Sale) return request;

        var automatic = request.Items
            .Where(i => products[i.ProductId].Tracking == ContainerTracking.ByBalance)
            .Select(i => new ContainerLine(i.ProductId, i.Quantity)).ToList();

        // Compatibilidad con clientes que aun mandan ContainersOut: no se suman dos
        // veces y no se permiten cantidades distintas de las vendidas.
        foreach (var supplied in request.ContainersOut)
            if (!automatic.Any(c => c.ProductId == supplied.ProductId && c.Quantity == supplied.Quantity))
                throw new BadRequestException("Los envases entregados deben coincidir con los productos retornables vendidos.");

        return request with { ContainersOut = automatic };
    }

    /// <summary>Los productos que toca la visita, en una sola pasada.</summary>
    private async Task<Dictionary<Guid, Product>> ResolverProductosAsync(
        RegisterDeliveryCommand request,
        CancellationToken cancellationToken)
    {
        var ids = request.Items.Select(i => i.ProductId)
            .Concat(request.ContainersOut.Select(c => c.ProductId))
            .Concat(request.ContainersIn.Select(c => c.ProductId))
            .Concat(request.LoansOut.Select(c => c.ProductId))
            .Concat(request.LoansIn.Select(c => c.ProductId))
            .Distinct()
            .ToList();

        var productos = new Dictionary<Guid, Product>();

        foreach (var id in ids)
            productos[id] = await _products.GetByIdAsync(id, cancellationToken)
                ?? throw new NotFoundException(nameof(Product), id);

        return productos;
    }

    /// <summary>
    /// Los envases seguidos por unidad, como el dispenser, no entran en la visita:
    /// cada uno tiene numero propio y se asigna por separado. Moverlos aca los
    /// contaria por cantidad y se perderia la identidad que los hace seguibles.
    /// </summary>
    private static void ValidarEnvases(
        RegisterDeliveryCommand request,
        IReadOnlyDictionary<Guid, Product> productos)
    {
        foreach (var linea in request.ContainersOut.Concat(request.ContainersIn))
        {
            var producto = productos[linea.ProductId];

            if (producto.Tracking == ContainerTracking.None)
                throw new BadRequestException($"'{producto.Detail}' no lleva seguimiento de envases.");

            if (producto.Tracking == ContainerTracking.ByUnit)
                throw new BadRequestException(
                    $"'{producto.Detail}' se sigue por numero de serie y se asigna por separado, " +
                    "no dentro de una visita.");
        }
    }

    /// <summary>
    /// Arma las lineas vendidas y las descuenta del camion. Devuelve el total.
    /// </summary>
    private async Task<decimal> AgregarVentaAsync(
        RegisterDeliveryCommand request,
        Delivery delivery,
        DeliverySession session,
        Guid customerId,
        DateTime ahora,
        Guid? usuario,
        CancellationToken cancellationToken)
    {
        var total = 0m;

        foreach (var linea in request.Items)
        {
            // Precio del cliente si tiene uno propio, y si no el del catalogo. Se
            // congela en la linea: un aumento posterior no puede reescribir esta venta.
            var precio = await _prices.GetPriceAsync(customerId, linea.ProductId, cancellationToken)
                  ?? (await _products.GetByIdAsync(linea.ProductId, cancellationToken))!.SalePrice;

            delivery.Items.Add(new DeliveryItem
            {
                ProductId = linea.ProductId,
                Quantity  = linea.Quantity,
                UnitPrice = precio
            });

            total += precio * linea.Quantity;

            session.StockMovements.Add(new SessionStockMovement
            {
                DeliverySessionId  = session.Id,
                ProductId          = linea.ProductId,
                State              = ContainerState.Full,
                Quantity           = -linea.Quantity,   // sale del camion
                Type               = SessionStockMovementType.Delivered,
                OccurredAt         = ahora,
                RegisteredByUserId = usuario
            });
        }

        return total;
    }

    /// <summary>
    /// Asienta el movimiento de envases en las dos direcciones y mueve el saldo del
    /// cliente y el stock del camion en el mismo acto.
    /// </summary>
    private async Task AgregarEnvasesAsync(
        RegisterDeliveryCommand request,
        Delivery delivery,
        DeliverySession session,
        Guid customerId,
        DateTime ahora,
        Guid? usuario,
        IReadOnlyList<(Guid ProductId, int Quantity, string Notes)> exchanges,
        CancellationToken cancellationToken)
    {
        // El neto por producto se acumula primero y se aplica una sola vez al final.
        // El mismo bidon suele estar en las dos listas (salen 10, vuelven 8): tocar el
        // saldo dos veces intenta crear dos filas para el mismo par cliente-producto,
        // porque la primera todavia no esta guardada y la segunda consulta no la ve.
        var deltas = new Dictionary<Guid, int>();
        foreach (var exchange in exchanges)
        {
            delivery.ContainerMovements.Add(new ContainerMovement
            {
                ProductId = exchange.ProductId, CustomerId = customerId, Quantity = exchange.Quantity,
                Type = ContainerMovementType.ExchangeCorrection, Notes = exchange.Notes,
                OccurredAt = ahora, RegisteredByUserId = usuario
            });
            Acumular(deltas, exchange.ProductId, exchange.Quantity);
        }

        foreach (var linea in request.ContainersOut)
        {
            delivery.ContainerMovements.Add(new ContainerMovement
            {
                ProductId          = linea.ProductId,
                CustomerId         = customerId,
                Quantity           = linea.Quantity,   // queda en poder del cliente
                Type               = ContainerMovementType.DeliveredToCustomer,
                OccurredAt         = ahora,
                RegisteredByUserId = usuario
            });

            Acumular(deltas, linea.ProductId, linea.Quantity);
        }

        foreach (var linea in request.ContainersIn)
        {
            delivery.ContainerMovements.Add(new ContainerMovement
            {
                ProductId          = linea.ProductId,
                CustomerId         = customerId,
                Quantity           = -linea.Quantity,  // vuelve
                Type               = ContainerMovementType.ReturnedFromCustomer,
                OccurredAt         = ahora,
                RegisteredByUserId = usuario
            });

            Acumular(deltas, linea.ProductId, -linea.Quantity);

            // El vacio sube al camion y baja recien al descargar en el deposito.
            session.StockMovements.Add(new SessionStockMovement
            {
                DeliverySessionId  = session.Id,
                ProductId          = linea.ProductId,
                State              = ContainerState.Empty,
                Quantity           = linea.Quantity,
                Type               = SessionStockMovementType.CollectedEmpty,
                OccurredAt         = ahora,
                RegisteredByUserId = usuario
            });
        }

        foreach (var (lines, returning) in new[] { (request.LoansOut, false), (request.LoansIn, true) })
            foreach (var line in lines)
            {
                var delta = returning ? -line.Quantity : line.Quantity;
                delivery.ContainerMovements.Add(new ContainerMovement
                {
                    ProductId = line.ProductId, CustomerId = customerId, Quantity = delta,
                    Type = returning ? ContainerMovementType.LoanReturned : ContainerMovementType.LoanedToCustomer,
                    OccurredAt = ahora, RegisteredByUserId = usuario
                });
                session.StockMovements.Add(new SessionStockMovement
                {
                    DeliverySessionId = session.Id, ProductId = line.ProductId, Quantity = -delta,
                    State = returning ? ContainerState.Empty : ContainerState.Full,
                    Type = returning ? SessionStockMovementType.CollectedEmpty : SessionStockMovementType.Delivered,
                    OccurredAt = ahora, RegisteredByUserId = usuario
                });
                Acumular(deltas, line.ProductId, delta);
            }

        // Siempre en el mismo orden: dos visitas simultáneas al mismo cliente no se bloquean entre sí.
        foreach (var (productId, delta) in deltas.OrderBy(d => d.Key))
        {
            // Neto cero no mueve el saldo: se llevo tantos como devolvio.
            if (delta == 0) continue;
            await _balances.AdjustAsync(customerId, productId, delta, cancellationToken);
        }
    }

    private static void Acumular(Dictionary<Guid, int> deltas, Guid productId, int delta)
        => deltas[productId] = deltas.GetValueOrDefault(productId) + delta;

    private async Task<Customer> ResolverClienteAsync(
        RegisterDeliveryCommand request,
        DeliverySession session,
        CancellationToken cancellationToken)
    {
        if (request.CustomerId is not null)
        {
            var existente = await _customers.GetByIdAsync(request.CustomerId.Value, cancellationToken)
                ?? throw new NotFoundException(nameof(Customer), request.CustomerId.Value);

            if (existente.RemovalRequests.Any(r => r.Status == RemovalStatus.Pending))
                throw new ConflictException("El cliente fue dado de baja; lo confirma la oficina");

            if (!existente.IsActive)
                throw new ConflictException("El cliente esta desactivado.");

            CustomerRouteAccess.EnsureMatchesSession(existente, session);
            return existente;
        }

        var datos = request.NewCustomer!;

        // Zona y lugar en el recorrido salen de la sesion: el cliente que se da de
        // alta en la calle pertenece a una de las zonas que se estan repartiendo (la
        // que eligio el chofer, o la principal) y queda al final de ese recorrido.
        var zona = CustomerRouteAccess.ResolveZone(session, datos.ZoneId);
        var nuevo = await StreetCustomer.CreateAsync(datos, session.VehicleId, zona, _customers, cancellationToken);

        // Se guarda antes que la visita para tener su Id: la entrega lo necesita como
        // clave foranea.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return nuevo;
    }
}
