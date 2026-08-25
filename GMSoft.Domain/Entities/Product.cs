using GMSoft.Domain.Common;
using GMSoft.Domain.Enums;

namespace GMSoft.Domain.Entities;

/// <summary>
/// Producto del catálogo de venta. El admin los da de alta y decide cuáles
/// quedan disponibles para el reparto.
/// </summary>
public class Product : BaseEntity
{
    /// <summary>Detalle interno, el que usa el negocio para identificar el producto.</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>Detalle comercial, el que ve el cliente.</summary>
    public string? CommercialDetail { get; set; }

    /// <summary>
    /// Precio de venta actual. Va en decimal y no en float/double: en punto flotante
    /// binario los importes con centavos no son exactos y los redondeos se acumulan.
    /// Las entregas guardan su propia copia del precio, así un aumento no reescribe
    /// la historia.
    /// </summary>
    public decimal SalePrice { get; set; }

    /// <summary>
    /// Modo de seguimiento del envase. Distinto de None significa que el envase
    /// tiene que volver.
    /// </summary>
    public ContainerTracking Tracking { get; set; }

    /// <summary>Si está publicado, es decir, disponible para el reparto.</summary>
    public bool IsPublished { get; set; }

    /// <summary>
    /// Ruta o URL de la foto. La imagen vive en el storage de archivos, no en la
    /// base: en la fila queda solo la referencia.
    /// </summary>
    public string? ImageUrl { get; set; }
}
