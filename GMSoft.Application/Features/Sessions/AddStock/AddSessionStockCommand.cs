using MediatR;
using GMSoft.Application.Features.Sessions.Common;

namespace GMSoft.Application.Features.Sessions.AddStock;

/// <summary>
/// Recarga en ruta. La carga el admin cuando el chofer le avisa que se quedo sin
/// stock, porque el equipo que acerca la mercaderia no usa el sistema.
/// </summary>
public record AddSessionStockCommand(
    Guid    Id,
    IReadOnlyList<SessionRestockItem> Items,
    Guid ClientRequestId,
    string? Notes = null) : IRequest<SessionRestockDto>;

public record SessionRestockItem(Guid ProductId, int Quantity);
