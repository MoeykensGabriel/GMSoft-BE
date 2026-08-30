using FluentValidation;

namespace GMSoft.Application.Features.ContainerUnits.Recover;

public class RecoverContainerUnitCommandValidator : AbstractValidator<RecoverContainerUnitCommand>
{
    public RecoverContainerUnitCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        // El motivo termina en ContainerMovement.Notes, que acepta 500. Sin tope aca,
        // un texto mas largo pasa la validacion y falla al guardar como 500.
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
