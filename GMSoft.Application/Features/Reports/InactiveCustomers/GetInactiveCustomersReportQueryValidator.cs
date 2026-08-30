using FluentValidation;

namespace GMSoft.Application.Features.Reports.InactiveCustomers;

public class GetInactiveCustomersReportQueryValidator
    : AbstractValidator<GetInactiveCustomersReportQuery>
{
    public GetInactiveCustomersReportQueryValidator()
    {
        RuleFor(x => x.Days).GreaterThan(0).WithMessage("Los dias tienen que ser mayores a cero.");
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
