using GMSoft.Domain.Common;

namespace GMSoft.Domain.Entities;

/// <summary>Por qué un cliente dejó de comprar, según lo que le dijo al controlador.</summary>
public enum NotBuyingReason { BuysElsewhere, Moved, DoesNotWant, Other }

/// <summary>
/// El control de un cliente hecho por un controlador subido a una salida: cuántos envases
/// tiene de verdad y si sigue comprando. Uno por cliente y salida; si se vuelve a controlar,
/// se reemplaza. Los envases se corrigen en el momento con un ajuste en el libro.
/// </summary>
public class CustomerInspection : BaseEntity
{
    public Guid DeliverySessionId { get; set; }
    public DeliverySession DeliverySession { get; set; } = null!;

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    /// <summary>El controlador (su ficha de <see cref="Driver"/>).</summary>
    public Guid InspectorId { get; set; }
    public Driver Inspector { get; set; } = null!;

    public Guid InspectedByUserId { get; set; }
    public DateTime InspectedAt { get; set; }

    public bool StillBuys { get; set; }
    public NotBuyingReason? NotBuyingReason { get; set; }
    public string? Notes { get; set; }

    /// <summary>Lo que había en el sistema y lo que contó, por producto.</summary>
    public List<InspectionContainerCount> Containers { get; set; } = [];
}

public class InspectionContainerCount
{
    public Guid ProductId { get; set; }
    public int SystemQuantity { get; set; }
    public int RealQuantity { get; set; }
}
