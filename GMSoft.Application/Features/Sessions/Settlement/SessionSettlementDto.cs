namespace GMSoft.Application.Features.Sessions.Settlement;

/// <summary>
/// La rendicion de una salida, con las tres cifras que importan y que no son la
/// misma: lo que vendio, lo que cobro y lo que entrego.
/// </summary>
public record SessionSettlementDto(
    Guid      SessionId,

    /// <summary>Suma de las entregas de la sesion.</summary>
    decimal   TotalSold,

    /// <summary>Suma de los pagos cobrados durante la sesion.</summary>
    decimal   TotalCollected,

    /// <summary>Lo que el admin recibio y conto. Nulo si todavia no se rindio.</summary>
    decimal?  AmountReceived,

    /// <summary>
    /// Vendido menos cobrado. Es la deuda nueva que quedo en la calle y es normal:
    /// se vende a cuenta todos los dias.
    /// </summary>
    decimal   NewDebt,

    /// <summary>
    /// Cobrado menos entregado. Esto NO es normal: es plata que el chofer cobro y no
    /// llego. Nulo mientras no se haya rendido.
    /// </summary>
    decimal?  CashDifference,

    DateTime? ReceivedAt,
    string?   Notes);
