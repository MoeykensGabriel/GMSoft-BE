using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Access;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GMSoft.Application.Features.Auth.Login;

public class LoginCommandHandler(IIdentityService identityService, IAccessEventRepository accessEvents,
    IUnitOfWork unitOfWork, ICurrentUserService currentUser, ILogger<LoginCommandHandler> logger)
    : IRequestHandler<LoginCommand, AuthResult>
{
    public async Task<AuthResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var result = await identityService.LoginAsync(request.UserName, request.Password, cancellationToken);
        if (result.DriverId is Guid driverId)
        {
            // Anotar la entrada nunca puede dejar al chofer afuera.
            try
            {
                await accessEvents.AddAsync(new AccessEvent
                {
                    UserId = result.UserId, DriverId = driverId, OccurredAt = DateTime.UtcNow, Kind = AccessEventKind.Login,
                    Client = AccessDevice.Client(currentUser.UserAgent, request.Installed),
                    Device = AccessDevice.Describe(currentUser.UserAgent), DeviceId = request.DeviceId,
                }, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "No se pudo anotar la entrada del chofer {DriverId}", driverId);
            }
        }
        return result;
    }
}
