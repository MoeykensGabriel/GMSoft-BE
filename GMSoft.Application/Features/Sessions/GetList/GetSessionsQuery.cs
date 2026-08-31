using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Sessions.Common;
using MediatR;

namespace GMSoft.Application.Features.Sessions.GetList;

/// <summary>
/// El listado de salidas. Vehículo y fecha juntos son la liquidación por reparto:
/// qué hizo ese camión ese día.
/// </summary>
/// <param name="Date">
/// Día local del negocio, no UTC. Filtra por cuándo SALIÓ la sesión: es el día del
/// reparto aunque el cierre haya caído después de medianoche.
/// </param>
public record GetSessionsQuery(
    int       Page      = 1,
    int       PageSize  = 20,
    Guid?     DriverId  = null,
    Guid?     ZoneId    = null,
    Guid?     VehicleId = null,
    DateOnly? Date      = null) : IRequest<PagedResult<SessionDto>>;
