namespace GMSoft.Domain.Enums;

/// <summary>Estado de una sesión de reparto.</summary>
public enum SessionStatus
{
    /// <summary>El chofer salió y todavía no rindió.</summary>
    Open = 0,

    /// <summary>Volvió, se contó lo que trajo y quedó conciliada.</summary>
    Closed = 1,
    /// <summary>
    /// La oficina la anuló antes de que tuviera movimientos (por ejemplo, la abrió y el
    /// chofer no vino). Queda oculta en listados e informes; la carga volvió a estar pendiente.
    /// </summary>
    Cancelled = 2
}
