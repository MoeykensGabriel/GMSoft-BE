using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Drivers.Common;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Drivers.GetById;

public class GetDriverByIdQueryHandler : IRequestHandler<GetDriverByIdQuery, DriverDto>
{
    private readonly IDriverRepository _drivers;

    public GetDriverByIdQueryHandler(IDriverRepository drivers)
    {
        _drivers = drivers;
    }

    public async Task<DriverDto> Handle(GetDriverByIdQuery request, CancellationToken cancellationToken)
    {
        var driver = await _drivers.GetWithVehicleAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Driver), request.Id);

        return DriverMapping.ToDto(driver);
    }
}
