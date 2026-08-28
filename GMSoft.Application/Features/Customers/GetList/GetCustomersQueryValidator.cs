using FluentValidation;

namespace GMSoft.Application.Features.Customers.GetList;

public class GetCustomersQueryValidator : AbstractValidator<GetCustomersQuery>
{
    public GetCustomersQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.InactiveSinceDays)
            .GreaterThan(0).When(x => x.InactiveSinceDays is not null)
            .WithMessage("Los dias sin comprar tienen que ser mayores a cero.");
    }
}
