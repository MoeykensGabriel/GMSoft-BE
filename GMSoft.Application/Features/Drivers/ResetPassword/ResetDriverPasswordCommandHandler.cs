using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Drivers.ResetPassword;

public class ResetDriverPasswordCommandHandler : IRequestHandler<ResetDriverPasswordCommand>
{
    private readonly IDriverRepository _drivers;
    private readonly IIdentityService _identityService;
    private readonly IPasswordVault _vault;
    private readonly IUnitOfWork _unitOfWork;

    public ResetDriverPasswordCommandHandler(
        IDriverRepository drivers,
        IIdentityService identityService,
        IPasswordVault vault,
        IUnitOfWork unitOfWork)
    {
        _drivers         = drivers;
        _identityService = identityService;
        _vault           = vault;
        _unitOfWork      = unitOfWork;
    }

    public async Task Handle(ResetDriverPasswordCommand request, CancellationToken cancellationToken)
    {
        var driver = await _drivers.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Driver), request.Id);

        if (driver.ApplicationUserId is null)
            throw new BadRequestException("Este chofer no tiene cuenta para entrar al sistema.");

        await _identityService.SetPasswordAsync(
            driver.ApplicationUserId.Value, request.NewPassword, cancellationToken);

        // Después del cambio en Identity: si ese falla, la lista no muestra una contraseña que no sirve.
        driver.PasswordCipher = _vault.Protect(request.NewPassword);
        _drivers.Update(driver);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
