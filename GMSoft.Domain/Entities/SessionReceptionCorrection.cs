using GMSoft.Domain.Common;
using GMSoft.Domain.Enums;

namespace GMSoft.Domain.Entities;

/// <summary>
/// Una correccion de la recepcion de una salida ya cerrada: lo que se anoto mal al
/// recibir el camion (lo descargado, el kilometraje o la plata rendida).
///
/// No se pisa nada. Lo descargado se corrige con un movimiento nuevo en el libro
/// mayor, y aca queda el antes y el despues de cada dato, con quien y por que.
/// </summary>
public class SessionReceptionCorrection : BaseEntity
{
    public Guid DeliverySessionId { get; set; }
    public DeliverySession DeliverySession { get; set; } = null!;

    public DateTime CorrectedAt { get; set; }
    public Guid? CorrectedByUserId { get; set; }

    /// <summary>Obligatorio: una correccion sin motivo no se puede revisar despues.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Nulos si el kilometraje no se toco.</summary>
    public int? PreviousKilometers { get; set; }
    public int? NewKilometers { get; set; }

    /// <summary>Nulos si la plata rendida no se toco.</summary>
    public decimal? PreviousAmountReceived { get; set; }
    public decimal? NewAmountReceived { get; set; }

    public ICollection<SessionReceptionCorrectionLine> Lines { get; set; }
        = new List<SessionReceptionCorrectionLine>();
}

/// <summary>Un producto y estado cuya descarga se corrigio: cuanto se habia anotado y cuanto volvio.</summary>
public class SessionReceptionCorrectionLine : BaseEntity
{
    public Guid SessionReceptionCorrectionId { get; set; }
    public SessionReceptionCorrection Correction { get; set; } = null!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public ContainerState State { get; set; }

    public int PreviousQuantity { get; set; }
    public int NewQuantity { get; set; }
}
