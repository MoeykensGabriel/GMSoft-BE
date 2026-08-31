using GMSoft.Domain.Common;

namespace GMSoft.Domain.Entities;

/// <summary>
/// Lo que la oficina sube a un camión ANTES de que salga.
///
/// El stock del camión durante el reparto vive en <see cref="SessionStockMovement"/>,
/// que cuelga de la sesión; pero la carga se hace de mañana, cuando todavía no hay
/// sesión ni se sabe qué chofer va a llevarlo. Esta tabla cubre ese hueco: es lo que
/// está arriba del camión esperando salir.
///
/// Es un libro mayor y no una foto por producto: interesa quién cargó qué y cuándo, y
/// con qué salida se fue. Al abrir la sesión, cada fila pendiente se convierte en un
/// movimiento de carga inicial y queda marcada con esa salida, así no se puede
/// consumir dos veces.
/// </summary>
public class VehicleLoad : BaseEntity
{
    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    /// <summary>Unidades llenas que se subieron. Siempre positivo.</summary>
    public int Quantity { get; set; }

    public DateTime LoadedAt { get; set; }

    /// <summary>Quién la cargó. Es la oficina, y conviene que quede asentado.</summary>
    public Guid? RegisteredByUserId { get; set; }

    /// <summary>
    /// La salida que se la llevó. Nulo mientras el camión sigue en el depósito: eso
    /// es exactamente "lo que está cargado y todavía no salió".
    /// </summary>
    public Guid? ConsumedBySessionId { get; set; }
    public DeliverySession? ConsumedBySession { get; set; }
}
