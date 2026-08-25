using GMSoft.Domain.Common;

namespace GMSoft.Domain.Entities;

/// <summary>
/// Un producto dentro de la carga de una sesión: cuánto salió y cuánto volvió.
/// Es lo que permite cerrar el día — salieron 100 bidones, volvieron 12 llenos y
/// 85 vacíos, y contra las entregas se ve si falta alguno.
/// </summary>
public class SessionLoadItem : BaseEntity
{
    public Guid DeliverySessionId { get; set; }
    public DeliverySession DeliverySession { get; set; } = null!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    /// <summary>Unidades que salieron en el vehículo.</summary>
    public int QuantityLoaded { get; set; }

    /// <summary>Unidades llenas que volvieron. Nulo mientras la sesión está abierta.</summary>
    public int? QuantityReturnedFull { get; set; }

    /// <summary>Envases vacíos que volvieron. Nulo mientras la sesión está abierta.</summary>
    public int? QuantityReturnedEmpty { get; set; }
}
