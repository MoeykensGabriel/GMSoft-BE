using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Drivers.Common;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Drivers.GetMe;

public class GetMyDriverProfileQueryHandler : IRequestHandler<GetMyDriverProfileQuery, DriverDto>
{
    private readonly IDriverRepository _drivers;
    private readonly ICurrentUserService _currentUser;

    public GetMyDriverProfileQueryHandler(
        IDriverRepository drivers,
        ICurrentUserService currentUser)
    {
        _drivers     = drivers;
        _currentUser = currentUser;
    }

    public async Task<DriverDto> Handle(
        GetMyDriverProfileQuery request,
        CancellationToken cancellationToken)
    {
        var driverId = _currentUser.DriverId
            ?? throw new ForbiddenException("Esta cuenta no tiene perfil de chofer.");

        var driver = await _drivers.GetWithVehicleAsync(driverId, cancellationToken)
            ?? throw new NotFoundException(nameof(Driver), driverId);

        return DriverMapping.ToDto(driver, _currentUser.UserName);
    }
}
