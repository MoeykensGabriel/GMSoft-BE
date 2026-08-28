using GMSoft.Application.Features.Drivers.Common;
using MediatR;

namespace GMSoft.Application.Features.Drivers.GetById;

public record GetDriverByIdQuery(Guid Id) : IRequest<DriverDto>;
