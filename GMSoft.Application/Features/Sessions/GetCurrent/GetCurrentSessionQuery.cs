using GMSoft.Application.Features.Sessions.Common;
using MediatR;

namespace GMSoft.Application.Features.Sessions.GetCurrent;

/// <summary>La sesion abierta del chofer que hace el request. Nula si no tiene.</summary>
public record GetCurrentSessionQuery : IRequest<SessionDto?>;
