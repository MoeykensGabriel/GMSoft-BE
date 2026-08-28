using FluentValidation;

namespace GMSoft.Application.Features.ContainerUnits.Assign;

public class AssignContainerUnitCommandValidator : AbstractValidator<AssignContainerUnitCommand>
{
    public AssignContainerUnitCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
