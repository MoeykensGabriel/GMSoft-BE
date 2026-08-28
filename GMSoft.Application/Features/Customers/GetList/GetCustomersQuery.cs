using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Customers.Common;
using MediatR;

namespace GMSoft.Application.Features.Customers.GetList;

/// <summary>
/// Con ZoneId es la hoja de ruta del chofer, en orden de recorrido. Sin zona es la
/// vista de la oficina, ordenada por nombre.
/// </summary>
public record GetCustomersQuery(
    int     Page       = 1,
    int     PageSize   = 20,
    string? Search     = null,
    Guid?   ZoneId     = null,
    bool?   OnlyActive = null,

    /// <summary>
    /// Solo los que hace mas de N dias que no compran, incluidos los que nunca
    /// compraron. Es la lista para salir a recuperar clientes.
    /// </summary>
    int?    InactiveSinceDays = null) : IRequest<PagedResult<CustomerDto>>;
