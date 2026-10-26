using FluentValidation;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Drivers.ChangeUserName;

/// <summary>
/// El admin le cambia el usuario con el que entra el chofer. Lo que hizo antes sigue a su
/// nombre: ventas, salidas y auditoría guardan la cuenta por su identificador, no por el
/// usuario. Le cierra la sesión, porque tiene que volver a entrar con el usuario nuevo.
/// </summary>
public record ChangeDriverUserNameCommand(Guid Id, string UserName) : IRequest;

public class ChangeDriverUserNameCommandValidator : AbstractValidator<ChangeDriverUserNameCommand>
{
    public ChangeDriverUserNameCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        // Las mismas reglas que al crear el chofer.
        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("El usuario es obligatorio: es con lo que entra al sistema.")
            .MaximumLength(50)
            .Matches("^[a-zA-Z0-9._-]+$")
            .WithMessage("El usuario solo puede tener letras, numeros, punto, guion y guion bajo.");
    }
}

public class ChangeDriverUserNameCommandHandler(IDriverRepository drivers, IIdentityService identity)
    : IRequestHandler<ChangeDriverUserNameCommand>
{
    public async Task Handle(ChangeDriverUserNameCommand request, CancellationToken cancellationToken)
    {
        var driver = await drivers.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Driver), request.Id);
        if (driver.ApplicationUserId is null)
            throw new BadRequestException("Este chofer no tiene cuenta para entrar al sistema.");

        await identity.SetUserNameAsync(driver.ApplicationUserId.Value, request.UserName, cancellationToken);
    }
}
