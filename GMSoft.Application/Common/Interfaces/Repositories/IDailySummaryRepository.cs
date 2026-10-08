using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;

namespace GMSoft.Application.Common.Interfaces.Repositories;

public record DailySummaryPayment(Guid SessionId, PaymentMethod Method, decimal Amount);
public record DailySummaryData(IReadOnlyList<DeliverySession> Sessions,
    IReadOnlyList<DailySummaryPayment> Payments);

public interface IDailySummaryRepository
{
    // Rango de apertura [desde, hasta), sin paginacion. Solo lectura.
    Task<DailySummaryData> GetAsync(Guid vehicleId, DateTime fromUtc, DateTime toUtc,
        CancellationToken cancellationToken = default);
}
