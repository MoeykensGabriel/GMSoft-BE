namespace GMSoft.Domain.Enums;

/// <summary>Que fue a hacer el chofer a la puerta del cliente.</summary>
public enum DeliveryType
{
    /// <summary>Le vendio algo. Puede ademas haber retirado vacios.</summary>
    Sale = 0,

    /// <summary>Paso solo a retirar envases, sin venta.</summary>
    ContainerPickup = 1
}
