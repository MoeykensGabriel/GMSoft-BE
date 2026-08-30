using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Reports.Common;
using MediatR;

namespace GMSoft.Application.Features.Reports.Debtors;

public record GetDebtorsReportQuery(
    int   Page     = 1,
    int   PageSize = 20,
    Guid? ZoneId   = null) : IRequest<PagedResult<DebtorLineDto>>;
