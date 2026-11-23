using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Drivers.Common;
using MediatR;

namespace GMSoft.Application.Features.Drivers.GetList;

public record GetDriversQuery(
    int     Page       = 1,
    int     PageSize   = 20,
    string? Search     = null,
    bool?   OnlyActive = null,
    /// <summary>false: solo choferes (para elegir quién sale); true: solo controladores; nulo: todos.</summary>
    bool?   Inspectors = null) : IRequest<PagedResult<DriverDto>>;
