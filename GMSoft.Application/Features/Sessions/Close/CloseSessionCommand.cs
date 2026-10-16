using GMSoft.Domain.Enums;
using MediatR;
using GMSoft.Application.Features.Sessions.Common;

namespace GMSoft.Application.Features.Sessions.Close;

/// <summary>
/// Cierre de la salida: kilometraje de vuelta, lo que se descarga en el deposito
/// (llenos que sobraron y vacios que se juntaron) y la plata que entrego el chofer.
///
/// La plata va en el mismo pedido para que la recepcion y la rendicion se guarden
/// juntas. Es opcional solo para cuando el chofer rinde mas tarde: entonces la salida
/// queda "falta rendir" y se carga despues desde la liquidacion.
/// </summary>
public record CloseSessionCommand(
    Guid Id,
    int  KilometersAtClose,
    IReadOnlyList<CloseSessionReturnLine> Returns,
    CloseSessionSettlement? Settlement = null) : IRequest<CloseSessionResult>;

public record CloseSessionReturnLine(Guid ProductId, ContainerState State, int Quantity);

/// <summary>El efectivo que el chofer entrego al volver.</summary>
public record CloseSessionSettlement(decimal AmountReceived, string? Notes);

/// <summary>
/// El resultado devuelve el stock que quedo colgado. Si no da todo cero, eso es el
/// faltante: se informa y queda a la vista del admin, no se descuenta a nadie.
/// </summary>
public record CloseSessionResult(
    Guid SessionId,
    bool CuadraTodo,
    IReadOnlyList<SessionStockLineDto> Faltante)
{
    /// <summary>Efectivo cobrado menos entregado. Nulo si la plata todavia no se rindio.</summary>
    public decimal? CashDifference { get; init; }
}
