using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Reports.Common;
using MediatR;

namespace GMSoft.Application.Features.Reports.InactiveCustomers;

/// <summary>
/// Clientes que hace mas de N dias que no compran. Trae ademas cuanto deben y
/// cuantos envases tienen: el que se fue con envases tuyos y debiendo es al que hay
/// que ir a buscar primero.
/// </summary>
public record GetInactiveCustomersReportQuery(
    int   Days     = 30,
    int   Page     = 1,
    int   PageSize = 20,
    Guid? ZoneId   = null) : IRequest<PagedResult<InactiveCustomerLineDto>>;
