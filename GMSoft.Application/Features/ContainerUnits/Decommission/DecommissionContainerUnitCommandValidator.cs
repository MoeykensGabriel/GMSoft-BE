using FluentValidation;

namespace GMSoft.Application.Features.ContainerUnits.Decommission;

public class DecommissionContainerUnitCommandValidator
    : AbstractValidator<DecommissionContainerUnitCommand>
{
    public DecommissionContainerUnitCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Hay que decir por que se da de baja la unidad.")
            .MaximumLength(500);
    }
}
