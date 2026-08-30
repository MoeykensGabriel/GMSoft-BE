using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Reports.Common;

namespace GMSoft.Application.Common.Interfaces.Repositories;

/// <summary>
/// Consultas de lectura para los reportes. Van agrupadas aparte de los repositorios
/// por agregado porque cruzan varias tablas y no pertenecen a ninguno en particular.
/// </summary>
public interface IReportRepository
{
    /// <summary>Envases en poder de clientes, por producto.</summary>
    Task<IReadOnlyList<ContainersOutLineDto>> GetContainersOutAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Clientes con saldo deudor, del que mas debe al que menos.</summary>
    Task<PagedResult<DebtorLineDto>> GetDebtorsAsync(
        int page,
        int pageSize,
        Guid? zoneId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clientes que hace mas de N dias que no compran, del mas caido al menos.
    /// Los que nunca compraron entran al final.
    /// </summary>
    Task<PagedResult<InactiveCustomerLineDto>> GetInactiveCustomersAsync(
        int page,
        int pageSize,
        int days,
        Guid? zoneId,
        CancellationToken cancellationToken = default);
}
