using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Reports.Common;
using MediatR;

namespace GMSoft.Application.Features.Reports.ContainersOut;

public class GetContainersOutReportQueryHandler
    : IRequestHandler<GetContainersOutReportQuery, IReadOnlyList<ContainersOutLineDto>>
{
    private readonly IReportRepository _reports;

    public GetContainersOutReportQueryHandler(IReportRepository reports)
    {
        _reports = reports;
    }

    public async Task<IReadOnlyList<ContainersOutLineDto>> Handle(
        GetContainersOutReportQuery request,
        CancellationToken cancellationToken)
        => await _reports.GetContainersOutAsync(cancellationToken);
}
