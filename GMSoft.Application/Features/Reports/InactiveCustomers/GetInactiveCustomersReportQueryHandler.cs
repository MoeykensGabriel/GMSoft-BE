using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Reports.Common;
using MediatR;

namespace GMSoft.Application.Features.Reports.InactiveCustomers;

public class GetInactiveCustomersReportQueryHandler
    : IRequestHandler<GetInactiveCustomersReportQuery, PagedResult<InactiveCustomerLineDto>>
{
    private readonly IReportRepository _reports;

    public GetInactiveCustomersReportQueryHandler(IReportRepository reports)
    {
        _reports = reports;
    }

    public async Task<PagedResult<InactiveCustomerLineDto>> Handle(
        GetInactiveCustomersReportQuery request,
        CancellationToken cancellationToken)
        => await _reports.GetInactiveCustomersAsync(
            request.Page, request.PageSize, request.Days, request.ZoneId, cancellationToken);
}
