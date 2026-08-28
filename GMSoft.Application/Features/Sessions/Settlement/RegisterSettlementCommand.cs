using MediatR;

namespace GMSoft.Application.Features.Sessions.Settlement;

/// <summary>
/// El admin carga cuanta plata del chofer le llego. Se compara contra lo COBRADO en
/// la sesion, no contra lo vendido: una venta a cuenta no trae plata, asi que medir
/// contra las ventas daria faltantes falsos todos los dias.
/// </summary>
public record RegisterSettlementCommand(
    Guid    Id,
    decimal AmountReceived,
    string? Notes) : IRequest<SessionSettlementDto>;
