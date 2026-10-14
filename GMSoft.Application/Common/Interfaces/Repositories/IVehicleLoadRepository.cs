using GMSoft.Domain.Entities;

namespace GMSoft.Application.Common.Interfaces.Repositories;

public interface IVehicleLoadRepository : IRepository<VehicleLoad>
{
    /// <summary>
    /// Lo que el camion tiene cargado y todavia no salio, con el producto incluido
    /// para poder mostrarlo. Es la carga que se va a llevar la proxima salida.
    /// </summary>
    Task<IReadOnlyList<VehicleLoad>> GetPendingAsync(
        Guid vehicleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lo último que se bajó del camión antes de salir, de lo más reciente a lo más
    /// viejo. Son filas eliminadas: se leen aparte de la carga pendiente.
    /// </summary>
    Task<IReadOnlyList<VehicleLoad>> GetRemovedAsync(
        Guid vehicleId,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cuándo se registró la tanda con ese identificador, o null si nunca llegó.
    /// Cuenta también las filas que después se bajaron: la tanda ya fue procesada.
    /// </summary>
    Task<DateTime?> GetLoadedAtByClientRequestAsync(
        Guid clientRequestId,
        CancellationToken cancellationToken = default);
}
