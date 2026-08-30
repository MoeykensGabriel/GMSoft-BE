using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Reports.Common;
using MediatR;

namespace GMSoft.Application.Features.Reports.Debtors;

public class GetDebtorsReportQueryHandler
    : IRequestHandler<GetDebtorsReportQuery, PagedResult<DebtorLineDto>>
{
    private readonly IReportRepository _reports;

    public GetDebtorsReportQueryHandler(IReportRepository reports)
    {
        _reports = reports;
    }

    public async Task<PagedResult<DebtorLineDto>> Handle(
        GetDebtorsReportQuery request,
        CancellationToken cancellationToken)
        => await _reports.GetDebtorsAsync(
            request.Page, request.PageSize, request.ZoneId, cancellationToken);
}
