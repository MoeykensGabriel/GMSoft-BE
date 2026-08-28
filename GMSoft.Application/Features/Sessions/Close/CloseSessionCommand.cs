using GMSoft.Domain.Enums;
using MediatR;
using GMSoft.Application.Features.Sessions.Common;

namespace GMSoft.Application.Features.Sessions.Close;

/// <summary>
/// Cierre de la salida: kilometraje de vuelta y lo que se descarga en el deposito,
/// tanto llenos que sobraron como vacios que se juntaron.
/// </summary>
public record CloseSessionCommand(
    Guid Id,
    int  KilometersAtClose,
    IReadOnlyList<CloseSessionReturnLine> Returns) : IRequest<CloseSessionResult>;

public record CloseSessionReturnLine(Guid ProductId, ContainerState State, int Quantity);

/// <summary>
/// El resultado devuelve el stock que quedo colgado. Si no da todo cero, eso es el
/// faltante: se informa y queda a la vista del admin, no se descuenta a nadie.
/// </summary>
public record CloseSessionResult(
    Guid SessionId,
    bool CuadraTodo,
    IReadOnlyList<SessionStockLineDto> Faltante);
