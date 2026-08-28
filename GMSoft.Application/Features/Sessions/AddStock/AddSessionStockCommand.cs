using MediatR;

namespace GMSoft.Application.Features.Sessions.AddStock;

/// <summary>
/// Recarga en ruta. La carga el admin cuando el chofer le avisa que se quedo sin
/// stock, porque el equipo que acerca la mercaderia no usa el sistema.
/// </summary>
public record AddSessionStockCommand(
    Guid    Id,
    Guid    ProductId,
    int     Quantity,
    string? Notes) : IRequest;
