using FluentValidation;

namespace GMSoft.Application.Features.Zones.GetList;

public class GetZonesQueryValidator : AbstractValidator<GetZonesQuery>
{
    public GetZonesQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
