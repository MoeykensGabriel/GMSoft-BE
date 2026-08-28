namespace GMSoft.Application.Features.Sessions.Common;

/// <summary>
/// Lo que hay a bordo de un producto, calculado sumando el libro mayor de la sesion.
/// Llenos y vacios se cuentan aparte porque son dos stocks distintos.
///
/// En una sesion cerrada estos numeros tendrian que dar cero: lo que quede es el
/// faltante, y por eso no se guarda en ningun campo, se lee de aca.
/// </summary>
public record SessionStockLineDto(
    Guid   ProductId,
    string ProductDetail,
    int    FullOnBoard,
    int    EmptyOnBoard);
