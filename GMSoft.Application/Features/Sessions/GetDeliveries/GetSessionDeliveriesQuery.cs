using GMSoft.Application.Features.Sessions.Common;
using MediatR;

namespace GMSoft.Application.Features.Sessions.GetDeliveries;

/// <summary>El recorrido de una salida: sus visitas en el orden en que se hicieron.</summary>
public record GetSessionDeliveriesQuery(Guid Id) : IRequest<IReadOnlyList<SessionDeliveryDto>>;
