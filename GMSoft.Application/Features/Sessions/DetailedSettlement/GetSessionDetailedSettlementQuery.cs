using MediatR;

namespace GMSoft.Application.Features.Sessions.DetailedSettlement;

/// <summary>La liquidacion de una salida cliente por cliente, en el orden en que se los visito.</summary>
public record GetSessionDetailedSettlementQuery(Guid Id) : IRequest<IReadOnlyList<CustomerSettlementDto>>;
