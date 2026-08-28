using FluentValidation;

namespace GMSoft.Application.Features.Drivers.ResetPassword;

public class ResetDriverPasswordCommandValidator : AbstractValidator<ResetDriverPasswordCommand>
{
    public ResetDriverPasswordCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8).WithMessage("La contraseña necesita al menos 8 caracteres.")
            .Matches("[A-Z]").WithMessage("La contraseña necesita al menos una mayúscula.")
            .Matches("[0-9]").WithMessage("La contraseña necesita al menos un número.");
    }
}
