using GMSoft.Application.Features.Drivers.Common;
using MediatR;

namespace GMSoft.Application.Features.Drivers.GetMe;

/// <summary>
/// El perfil del chofer que hace el request. No recibe id a proposito: se resuelve
/// del token, asi nadie puede pedir la ficha de otro pasando su id.
/// </summary>
public record GetMyDriverProfileQuery : IRequest<DriverDto>;
