using FluentValidation;

namespace GMSoft.Application.Features.ContainerUnits.Update;

public class UpdateContainerUnitCommandValidator : AbstractValidator<UpdateContainerUnitCommand>
{
    public UpdateContainerUnitCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.SerialNumber).NotEmpty().MaximumLength(50);
    }
}
