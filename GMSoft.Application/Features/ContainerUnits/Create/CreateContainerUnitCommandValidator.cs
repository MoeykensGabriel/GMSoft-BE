using FluentValidation;

namespace GMSoft.Application.Features.ContainerUnits.Create;

public class CreateContainerUnitCommandValidator : AbstractValidator<CreateContainerUnitCommand>
{
    public CreateContainerUnitCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.SerialNumber).NotEmpty().MaximumLength(50);
    }
}
