using MediatR;

namespace GMSoft.Application.Features.Sessions.Settlement;

public record GetSessionSettlementQuery(Guid Id) : IRequest<SessionSettlementDto>;
