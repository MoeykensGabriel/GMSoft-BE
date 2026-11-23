using GMSoft.Domain.Common;
using GMSoft.Domain.Enums;

namespace GMSoft.Domain.Entities;

/// <summary>
/// Una entrada o salida de un chofer en la app. Sirve para entender por qué a alguien
/// "se le sale solo el sistema": cada entrada, cada cierre y cada rechazo del servidor.
/// </summary>
public class AccessEvent : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid DriverId { get; set; }
    public DateTime OccurredAt { get; set; }
    public AccessEventKind Kind { get; set; }

    /// <summary>Solo en salidas y rechazos.</summary>
    public AccessEndReason? Reason { get; set; }

    public AccessClient Client { get; set; }

    /// <summary>Celular y navegador, resumidos del User-Agent ("Android 14 · Chrome 130").</summary>
    public string? Device { get; set; }

    /// <summary>
    /// Identificador que la app guarda en el celular. Si cambia entre dos entradas, el
    /// celular borró los datos de la página (o entró desde otro navegador).
    /// </summary>
    public Guid? DeviceId { get; set; }

    /// <summary>Id del token rechazado: un mismo token vencido se anota una sola vez.</summary>
    public string? TokenId { get; set; }
}
