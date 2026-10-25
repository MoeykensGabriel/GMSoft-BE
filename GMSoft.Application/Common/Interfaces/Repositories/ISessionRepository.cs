using GMSoft.Application.Features.Sessions.Common;
using GMSoft.Application.Features.Sessions.DetailedSettlement;
using GMSoft.Domain.Entities;

namespace GMSoft.Application.Common.Interfaces.Repositories;

public interface ISessionRepository : IRepository<DeliverySession>
{
    Task<IReadOnlyList<Delivery>> GetCustomerVisitsAsync(Guid sessionId, Guid customerId,
        CancellationToken cancellationToken = default);
    /// <summary>Todas las salidas abiertas, con datos de reparto y movimientos de carga inicial.</summary>
    /// <summary>Las salidas abiertas con todos sus movimientos de stock y el producto de cada uno.</summary>
    Task<IReadOnlyList<DeliverySession>> GetOpenWithStockAsync(CancellationToken cancellationToken = default);

    /// <summary>La sesion abierta de un chofer, si tiene. Un chofer no puede tener dos.</summary>
    Task<DeliverySession?> GetOpenByDriverAsync(Guid driverId, CancellationToken cancellationToken = default);

    /// <summary>Las zonas que recorre la salida, en orden; la primera es la principal.</summary>
    Task<IReadOnlyList<Guid>> GetZoneIdsAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// La salida aunque esté anulada (las anuladas quedan ocultas para todo lo demás): el
    /// chofer tiene que poder enterarse de que la oficina se la anuló.
    /// </summary>
    Task<DeliverySession?> GetByIdIncludingCancelledAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Bloquea y relee una salida, abierta o cerrada, dentro de una transaccion.</summary>
    Task<DeliverySession?> LockAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dentro de una transaccion: bloquea la fila de la salida hasta el commit y
    /// dice si sigue abierta. Serializa lo que mueve su stock (ventas, promociones,
    /// recargas y recepcion) para que ninguna operacion decida sobre un saldo viejo.
    /// </summary>
    Task<bool> LockOpenAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>Si el vehiculo ya esta en la calle con otra sesion abierta.</summary>
    Task<bool> HasOpenSessionForVehicleAsync(
        Guid vehicleId,
        Guid? excludeSessionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Trae la sesion con chofer, vehiculo y zona para armar el DTO.</summary>
    Task<DeliverySession?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stock a bordo, sumando los movimientos por producto y estado. Es la unica
    /// fuente: no hay un campo de stock que pueda quedar desincronizado.
    /// </summary>
    /// <summary>
    /// El stock que quedó colgado de varias salidas, en una sola consulta. En una salida
    /// recibida, todo en cero es que cuadró; lo que no, es el faltante o sobrante.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<SessionStockLineDto>>> GetStockBalancesAsync(
        IReadOnlyCollection<Guid> sessionIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SessionStockLineDto>> GetStockBalanceAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lo vendido y lo cobrado en la sesion. Son dos numeros distintos: lo que no se
    /// cobro quedo como deuda del cliente, no como faltante de caja.
    /// </summary>
    Task<(decimal Sold, decimal Collected)> GetMoneyTotalsAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Las visitas de la salida, en el orden en que se hicieron. Es el recorrido del
    /// dia que mira el admin.
    /// </summary>
    Task<IReadOnlyList<SessionDeliveryDto>> GetDeliveriesAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>Lo cobrado en la salida, sumado por cliente y medio de pago.</summary>
    Task<IReadOnlyList<SessionCustomerPaymentDto>> GetPaymentsByCustomerAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saldo de cuenta de cada cliente visitado en la salida, contando ventas y
    /// cobros hasta <paramref name="untilUtc"/> inclusive.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetCustomerBalancesAsync(
        Guid sessionId,
        DateTime untilUtc,
        CancellationToken cancellationToken = default);

    /// <summary>La rendicion de la sesion, o nula si todavia no se rindio.</summary>
    Task<SessionCashSettlement?> GetSettlementAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Salidas filtradas. El rango de fechas viene ya en UTC y cerrado por izquierda
    /// ([desde, hasta)): que dia local representa es decision de quien llama.
    /// El rango de cierre filtra por cuando se recibio el camion; con includeOpen
    /// se suman las salidas todavia abiertas, que van primero.
    /// </summary>
    Task<(IReadOnlyList<DeliverySession> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        Guid? driverId,
        Guid? zoneId,
        Guid? vehicleId,
        DateTime? openedFromUtc,
        DateTime? openedToUtc,
        CancellationToken cancellationToken = default,
        DateTime? closedFromUtc = null,
        DateTime? closedToUtc = null,
        bool includeOpen = false);

    /// <summary>La visita registrada con ese identificador del cliente, si ya llego.</summary>
    Task<Delivery?> GetDeliveryByClientRequestAsync(
        Guid clientRequestId,
        CancellationToken cancellationToken = default);
}
