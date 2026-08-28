using FluentValidation;

namespace GMSoft.Application.Features.Customers.Account;

public class GetCustomerAccountQueryValidator : AbstractValidator<GetCustomerAccountQuery>
{
    public GetCustomerAccountQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.MovementsLimit).InclusiveBetween(1, 200);
    }
}
