using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.ContainerUnits.Common;
using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.ContainerUnits.GetList;

/// <summary>
/// Con Status en WithCustomer es el listado de todo lo que esta en la calle, que es
/// la pregunta que importa: donde esta cada unidad.
/// </summary>
public record GetContainerUnitsQuery(
    int                  Page       = 1,
    int                  PageSize   = 20,
    string?              Search     = null,
    Guid?                ProductId  = null,
    ContainerUnitStatus? Status     = null,
    Guid?                CustomerId = null) : IRequest<PagedResult<ContainerUnitDto>>;
