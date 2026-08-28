using FluentValidation;

namespace GMSoft.Application.Features.Sessions.AddStock;

public class AddSessionStockCommandValidator : AbstractValidator<AddSessionStockCommand>
{
    public AddSessionStockCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
