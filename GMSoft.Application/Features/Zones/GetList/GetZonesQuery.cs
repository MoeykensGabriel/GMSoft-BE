using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Zones.Common;
using MediatR;

namespace GMSoft.Application.Features.Zones.GetList;

/// <summary>
/// OnlyActive en true es lo que ve el chofer en la pantalla de apertura de sesion:
/// no tiene sentido ofrecerle una zona dada de baja.
/// </summary>
public record GetZonesQuery(
    int     Page       = 1,
    int     PageSize   = 20,
    string? Search     = null,
    bool?   OnlyActive = null) : IRequest<PagedResult<ZoneDto>>;
