namespace GMSoft.Application.Features.VehicleLoads.Common;

/// <summary>
/// Una carga puesta arriba del camion. Va linea por linea y no sumada por producto
/// porque cada una se puede sacar por separado: la oficina carga en varias tandas y
/// se equivoca en una sola.
/// </summary>
public record VehicleLoadLineDto(
    Guid     Id,
    Guid     ProductId,
    string   ProductDetail,
    int      Quantity,
    DateTime LoadedAt);
