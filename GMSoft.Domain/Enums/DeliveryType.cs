namespace GMSoft.Domain.Enums;

/// <summary>Que fue a hacer el chofer a la puerta del cliente.</summary>
public enum DeliveryType
{
    /// <summary>Le vendio algo. Puede ademas haber movido envases.</summary>
    Sale = 0,

    /// <summary>
    /// Visita sin venta, solo movimiento de envases. Cubre tanto pasar a retirar
    /// vacios como dejarle un envase sin cobrarle nada.
    /// </summary>
    ContainerOnly = 1
}
