using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Drivers.Common;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Drivers.GetById;

public class GetDriverByIdQueryHandler : IRequestHandler<GetDriverByIdQuery, DriverDto>
{
    private readonly IDriverRepository _drivers;
    private readonly IIdentityService _identityService;

    public GetDriverByIdQueryHandler(IDriverRepository drivers, IIdentityService identityService)
    {
        _drivers         = drivers;
        _identityService = identityService;
    }

    public async Task<DriverDto> Handle(GetDriverByIdQuery request, CancellationToken cancellationToken)
    {
        var driver = await _drivers.GetWithVehicleAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Driver), request.Id);

        var usuarios = driver.ApplicationUserId is null
            ? new Dictionary<Guid, string>()
            : await _identityService.GetUserNamesAsync([driver.ApplicationUserId.Value], cancellationToken);

        return DriverMapping.ToDto(
            driver,
            driver.ApplicationUserId is not null && usuarios.TryGetValue(driver.ApplicationUserId.Value, out var u)
                ? u
                : null);
    }
}
