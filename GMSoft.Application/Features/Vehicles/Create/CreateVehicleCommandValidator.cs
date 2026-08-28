using FluentValidation;

namespace GMSoft.Application.Features.Vehicles.Create;

public class CreateVehicleCommandValidator : AbstractValidator<CreateVehicleCommand>
{
    public CreateVehicleCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LicensePlate).NotEmpty().MaximumLength(15);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.CurrentKilometers)
            .GreaterThanOrEqualTo(0).WithMessage("El kilometraje no puede ser negativo.");
    }
}
