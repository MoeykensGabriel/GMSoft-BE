using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;

namespace GMSoft.Application.Common.Interfaces.Repositories;

public record DailySummaryPayment(Guid SessionId, PaymentMethod Method, decimal Amount);
/// <summary>Vacíos que los clientes tenían sin registrar y devolvieron en una salida.</summary>
public record DailySummaryRecovered(Guid SessionId, Guid ProductId, int Quantity);
public record DailySummaryData(IReadOnlyList<DeliverySession> Sessions,
    IReadOnlyList<DailySummaryPayment> Payments)
{
    public IReadOnlyList<DailySummaryRecovered> Recovered { get; init; } = [];
}

public interface IDailySummaryRepository
{
    // Rango de apertura [desde, hasta), sin paginacion. Solo lectura.
    Task<DailySummaryData> GetAsync(Guid vehicleId, DateTime fromUtc, DateTime toUtc,
        CancellationToken cancellationToken = default);
}
