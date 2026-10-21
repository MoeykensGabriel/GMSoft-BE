using FluentValidation;

namespace GMSoft.Application.Features.Sessions.Open;

public class OpenSessionCommandValidator : AbstractValidator<OpenSessionCommand>
{
    public OpenSessionCommandValidator()
    {
        RuleFor(x => x.DriverId).NotEmpty().When(x => x.DriverId.HasValue);
        RuleFor(x => x.EffectiveZoneIds)
            .Must(ids => ids.Length > 0 && ids.All(id => id != Guid.Empty))
            .WithMessage("Hay que elegir la zona de reparto.")
            .Must(ids => ids.Distinct().Count() == ids.Length)
            .WithMessage("Cada zona va una sola vez.")
            .Must(ids => ids.Length <= OpenSessionCommand.MaxZones)
            .WithMessage($"Una salida recorre hasta {OpenSessionCommand.MaxZones} zonas.")
            .OverridePropertyName(nameof(OpenSessionCommand.ZoneIds));

        RuleFor(x => x.KilometersAtOpen)
            .GreaterThanOrEqualTo(0).WithMessage("El kilometraje no puede ser negativo.");
    }
}
