using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using MediatR;

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

    public RegisterDeliveryCommandHandler(
        ISessionRepository sessions,
        ICustomerRepository customers,
        IProductRepository products,
        ICustomerPriceRepository prices,
        IContainerBalanceRepository balances,
        IRepository<Payment> payments,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _sessions    = sessions;
        _customers   = customers;
        _products    = products;
        _prices      = prices;
        _balances    = balances;
        _payments    = payments;
        _currentUser = currentUser;
        _unitOfWork  = unitOfWork;
    }

    public async Task<RegisterDeliveryResult> Handle(
        RegisterDeliveryCommand request,
        CancellationToken cancellationToken)
    {
        var driverId = _currentUser.DriverId
            ?? throw new ForbiddenException("Solo un chofer registra visitas.");

        // La sesion no viene por parametro: es la abierta del chofer. Si viniera, se
        // podrian imputar entregas a una salida que no es la suya.
        var session = await _sessions.GetOpenByDriverAsync(driverId, cancellationToken)
            ?? throw new ConflictException(
                "No tenes una sesion de reparto abierta. Abri una antes de registrar visitas.");

        var productos = await ResolverProductosAsync(request, cancellationToken);
        ValidarEnvases(request, productos);

        var ahora   = DateTime.UtcNow;
        var usuario = _currentUser.UserId;

        var customerId = Guid.Empty;
        var deliveryId = Guid.Empty;
        var total      = 0m;

        // Todo junto o nada. Si se guardara la venta y fallaran los envases, el libro
        // mayor dejaria de explicar el saldo del cliente, que es justo lo que este
        // sistema existe para evitar.
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var customer = await ResolverClienteAsync(request, session, cancellationToken);
            customerId = customer.Id;

            var delivery = new Delivery
            {
                DeliverySessionId = session.Id,
                CustomerId        = customer.Id,
                Type              = request.Type,
                DeliveredAt       = ahora,
                Notes             = request.Notes?.Trim()
            };

            total = await AgregarVentaAsync(request, delivery, session, customer.Id, ahora, usuario, cancellationToken);
            delivery.Total = total;

            await AgregarEnvasesAsync(request, delivery, session, customer.Id, ahora, usuario, cancellationToken);

            session.Deliveries.Add(delivery);
            _sessions.Update(session);

            if (request.Payment is not null)
            {
                await _payments.AddAsync(new Payment
                {
                    CustomerId        = customer.Id,
                    DeliverySessionId = session.Id,
                    Amount            = request.Payment.Amount,
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

    /// <summary>Los productos que toca la visita, en una sola pasada.</summary>
    private async Task<Dictionary<Guid, Product>> ResolverProductosAsync(
        RegisterDeliveryCommand request,
        CancellationToken cancellationToken)
    {
        var ids = request.Items.Select(i => i.ProductId)
            .Concat(request.ContainersOut.Select(c => c.ProductId))
            .Concat(request.ContainersIn.Select(c => c.ProductId))
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
        CancellationToken cancellationToken)
    {
        // El neto por producto se acumula primero y se aplica una sola vez al final.
        // El mismo bidon suele estar en las dos listas (salen 10, vuelven 8): tocar el
        // saldo dos veces intenta crear dos filas para el mismo par cliente-producto,
        // porque la primera todavia no esta guardada y la segunda consulta no la ve.
        var deltas = new Dictionary<Guid, int>();

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

        foreach (var (productId, delta) in deltas)
        {
            // Neto cero no mueve el saldo: se llevo tantos como devolvio.
            if (delta == 0) continue;
            await AjustarSaldoAsync(customerId, productId, delta, cancellationToken);
        }
    }

    private static void Acumular(Dictionary<Guid, int> deltas, Guid productId, int delta)
        => deltas[productId] = deltas.GetValueOrDefault(productId) + delta;

    /// <summary>
    /// Mueve el saldo de envases del cliente. Es una foto del libro mayor y se
    /// actualiza en la misma transaccion que el movimiento, para que nunca exista
    /// uno sin el otro.
    /// </summary>
    private async Task AjustarSaldoAsync(
        Guid customerId,
        Guid productId,
        int delta,
        CancellationToken cancellationToken)
    {
        var saldo = await _balances.GetAsync(customerId, productId, cancellationToken);

        if (saldo is null)
        {
            await _balances.AddAsync(new CustomerContainerBalance
            {
                CustomerId = customerId,
                ProductId  = productId,
                Quantity   = delta
            }, cancellationToken);
            return;
        }

        saldo.Quantity += delta;
        _balances.Update(saldo);
    }

    private async Task<Customer> ResolverClienteAsync(
        RegisterDeliveryCommand request,
        DeliverySession session,
        CancellationToken cancellationToken)
    {
        if (request.CustomerId is not null)
        {
            var existente = await _customers.GetByIdAsync(request.CustomerId.Value, cancellationToken)
                ?? throw new NotFoundException(nameof(Customer), request.CustomerId.Value);

            if (!existente.IsActive)
                throw new ConflictException("El cliente esta desactivado.");

            return existente;
        }

        var datos = request.NewCustomer!;

        // Zona y lugar en el recorrido salen de la sesion: el cliente que se da de
        // alta en la calle pertenece a la zona que se esta repartiendo y queda al
        // final de ese recorrido.
        var nuevo = new Customer
        {
            BusinessName = string.IsNullOrWhiteSpace(datos.BusinessName) ? null : datos.BusinessName.Trim(),
            ContactName  = datos.ContactName.Trim(),
            Phone        = datos.Phone.Trim(),
            Address      = datos.Address.Trim(),
            Notes        = datos.Notes?.Trim(),
            ZoneId       = session.ZoneId,
            IsActive     = true,
            RouteOrder   = await _customers.GetNextRouteOrderAsync(session.ZoneId, cancellationToken)
        };

        await _customers.AddAsync(nuevo, cancellationToken);

        // Se guarda antes que la visita para tener su Id: la entrega lo necesita como
        // clave foranea.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return nuevo;
    }
}
