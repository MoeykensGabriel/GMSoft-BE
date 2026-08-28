using GMSoft.Domain.Common;

namespace GMSoft.Domain.Entities;

/// <summary>
/// Rendicion de una sesion: cuanta plata del chofer le llego efectivamente al
/// admin. Se compara contra lo que el chofer cobro (suma de pagos de la sesion).
///
/// La comparacion util es contra lo cobrado y no contra lo vendido: una venta a
/// cuenta no trae plata, asi que medir contra las ventas da faltantes falsos
/// todos los dias.
/// </summary>
public class SessionCashSettlement : BaseEntity
{
    public Guid DeliverySessionId { get; set; }
    public DeliverySession DeliverySession { get; set; } = null!;

    /// <summary>Lo que el admin recibio y conto.</summary>
    public decimal AmountReceived { get; set; }

    public DateTime ReceivedAt { get; set; }

    /// <summary>Quien lo recibio. Guid de la cuenta, sin navegacion.</summary>
    public Guid? ReceivedByUserId { get; set; }

    public string? Notes { get; set; }
}
