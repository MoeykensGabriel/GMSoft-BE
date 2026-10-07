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
/// <param name="ClosedDate">
/// Día local del negocio en que se RECIBIÓ el camión. Es otro filtro, no un sinónimo
/// de <paramref name="Date"/>: una salida de ayer recibida hoy pertenece a hoy.
/// </param>
/// <param name="IncludeOpen">
/// Junto con <paramref name="ClosedDate"/>, suma las salidas que siguen en la calle,
/// del día que sean. Sin eso el listado de recepciones esconde los camiones que
/// todavía hay que recibir.
/// </param>
public record GetSessionsQuery(
    int       Page        = 1,
    int       PageSize    = 20,
    Guid?     DriverId    = null,
    Guid?     ZoneId      = null,
    Guid?     VehicleId   = null,
    DateOnly? Date        = null,
    DateOnly? ClosedDate  = null,
    bool      IncludeOpen = false) : IRequest<PagedResult<SessionDto>>;
