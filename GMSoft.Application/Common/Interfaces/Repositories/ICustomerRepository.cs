using GMSoft.Domain.Entities;

namespace GMSoft.Application.Common.Interfaces.Repositories;

public interface ICustomerRepository : IRepository<Customer>
{
    /// <summary>
    /// Listado del reparto. Filtrado por zona y ordenado por RouteOrder es la hoja
    /// de ruta que ve el chofer; sin zona es la vista de la oficina.
    /// </summary>
    Task<(IReadOnlyList<Customer> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        Guid? zoneId,
        bool? onlyActive,
        int? inactiveSinceDays,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fecha de la ultima COMPRA de cada cliente pedido. Solo cuentan las visitas de
    /// venta: pasar a retirar envases no es una compra y no deberia hacer parecer
    /// activo a un cliente que dejo de comprar.
    ///
    /// Va en una consulta aparte y no por cliente, para no hacer una query por fila.
    /// El cliente que no aparece en el diccionario es el que nunca compro.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, DateTime>> GetLastPurchaseDatesAsync(
        IReadOnlyCollection<Guid> customerIds,
        CancellationToken cancellationToken = default);

    Task<Customer?> GetWithZoneAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Siguiente lugar libre del recorrido de una zona. El cliente nuevo va al final,
    /// que es lo que significa que el orden sea el orden de carga.
    /// </summary>
    Task<int> GetNextRouteOrderAsync(Guid zoneId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cuanto debe el cliente: suma de sus entregas menos suma de sus pagos. Se
    /// calcula y no se guarda, asi no hay un campo de saldo que se pueda desviar de
    /// los movimientos que lo explican.
    /// </summary>
    Task<decimal> GetAccountBalanceAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>Si ya tiene visitas, movimientos de envase o pagos.</summary>
    Task<bool> HasHistoryAsync(Guid id, CancellationToken cancellationToken = default);
}
