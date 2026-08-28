using FluentValidation;

namespace GMSoft.Application.Features.Sessions.GetList;

public class GetSessionsQueryValidator : AbstractValidator<GetSessionsQuery>
{
    public GetSessionsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
