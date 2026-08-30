using FluentValidation;

namespace GMSoft.Application.Features.Auth.Login;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("El usuario es obligatorio.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria.");
    }
}
