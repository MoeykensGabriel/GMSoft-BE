using FluentValidation;

namespace GMSoft.Application.Features.Drivers.GetList;

public class GetDriversQueryValidator : AbstractValidator<GetDriversQuery>
{
    public GetDriversQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
