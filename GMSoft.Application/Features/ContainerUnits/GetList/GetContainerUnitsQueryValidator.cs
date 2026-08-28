using FluentValidation;

namespace GMSoft.Application.Features.ContainerUnits.GetList;

public class GetContainerUnitsQueryValidator : AbstractValidator<GetContainerUnitsQuery>
{
    public GetContainerUnitsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
    }
}
