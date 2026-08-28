using FluentValidation;

namespace GMSoft.Application.Features.Drivers.Create;

public class CreateDriverCommandValidator : AbstractValidator<CreateDriverCommand>
{
    public CreateDriverCommandValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DocumentNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(30);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El email es obligatorio: es el usuario con el que entra.")
            .EmailAddress();

        // Mismo minimo que exige Identity. Se valida aca ademas para que el error
        // vuelva como 400 con el campo señalado y no como un mensaje suelto.
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8).WithMessage("La contraseña necesita al menos 8 caracteres.")
            .Matches("[A-Z]").WithMessage("La contraseña necesita al menos una mayúscula.")
            .Matches("[0-9]").WithMessage("La contraseña necesita al menos un número.");
    }
}
