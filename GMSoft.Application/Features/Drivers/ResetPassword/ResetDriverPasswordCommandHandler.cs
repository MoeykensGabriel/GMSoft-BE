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

    public ResetDriverPasswordCommandHandler(
        IDriverRepository drivers,
        IIdentityService identityService)
    {
        _drivers         = drivers;
        _identityService = identityService;
    }

    public async Task Handle(ResetDriverPasswordCommand request, CancellationToken cancellationToken)
    {
        var driver = await _drivers.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Driver), request.Id);

        if (driver.ApplicationUserId is null)
            throw new BadRequestException("Este chofer no tiene cuenta para entrar al sistema.");

        await _identityService.SetPasswordAsync(
            driver.ApplicationUserId.Value, request.NewPassword, cancellationToken);
    }
}
