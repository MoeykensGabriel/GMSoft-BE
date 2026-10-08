using FluentValidation;

namespace GMSoft.Application.Features.Sessions.AddStock;

public class AddSessionStockCommandValidator : AbstractValidator<AddSessionStockCommand>
{
    public AddSessionStockCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ClientRequestId).NotEmpty();
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).NotNull().ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty();
            item.RuleFor(i => i.Quantity).GreaterThan(0);
        });
        RuleFor(x => x.Items)
            .Must(items => items is null || items.Where(i => i is not null)
                .Select(i => i.ProductId).Distinct().Count() == items.Count)
            .WithMessage("Hay productos repetidos en la recarga.");
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
