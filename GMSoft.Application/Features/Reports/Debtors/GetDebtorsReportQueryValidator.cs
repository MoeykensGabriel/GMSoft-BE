using FluentValidation;

namespace GMSoft.Application.Features.Reports.Debtors;

public class GetDebtorsReportQueryValidator : AbstractValidator<GetDebtorsReportQuery>
{
    public GetDebtorsReportQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
