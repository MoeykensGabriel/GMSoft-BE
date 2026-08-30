using FluentValidation;

namespace GMSoft.Application.Features.Auth.Login;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("El usuario es obligatorio.")
            .MaximumLength(50);

        // Con tope: hashear una contraseña enorme cuesta CPU, y sin limite eso es una
        // forma barata de hacer trabajar al servidor desde afuera.
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MaximumLength(128);
    }
}
