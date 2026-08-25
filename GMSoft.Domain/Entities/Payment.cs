using GMSoft.Domain.Common;
using GMSoft.Domain.Enums;

namespace GMSoft.Domain.Entities;

/// <summary>
/// Un cobro al cliente. No se ata a una entrega puntual: baja el saldo de su
/// cuenta. Es lo que pasa en la calle — el cliente paga un monto que cubre lo de
/// hoy y algo de lo anterior.
/// </summary>
public class Payment : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    /// <summary>
    /// Sesión en la que se cobró, o sea qué chofer lo trajo. Nulo si el pago entró
    /// por fuera del reparto, por ejemplo una transferencia recibida en la oficina.
    /// </summary>
    public Guid? DeliverySessionId { get; set; }
    public DeliverySession? DeliverySession { get; set; }

    public decimal Amount { get; set; }

    public PaymentMethod Method { get; set; }

    public DateTime PaidAt { get; set; }

    public string? Notes { get; set; }
}
