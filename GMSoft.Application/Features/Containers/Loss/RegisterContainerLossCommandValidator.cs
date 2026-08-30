using FluentValidation;

namespace GMSoft.Application.Features.Containers.Loss;

public class RegisterContainerLossCommandValidator
    : AbstractValidator<RegisterContainerLossCommand>
{
    public RegisterContainerLossCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Hay que decir por que se dan por perdidos.")
            .MaximumLength(500);
    }
}
