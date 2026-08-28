namespace GMSoft.Domain.Enums;

/// <summary>Por que cambio el stock del camion durante una sesion.</summary>
public enum SessionStockMovementType
{
    /// <summary>Lo que se cargo al salir.</summary>
    InitialLoad = 0,

    /// <summary>Recarga en ruta. El camion se quedo sin stock y le acercaron mas.</summary>
    Restock = 1,

    /// <summary>Salio del camion hacia un cliente.</summary>
    Delivered = 2,

    /// <summary>Vacio que el cliente devolvio y subio al camion.</summary>
    CollectedEmpty = 3,

    /// <summary>Descargado en el deposito al cerrar la sesion.</summary>
    ReturnedAtClose = 4,

    /// <summary>Correccion manual. Siempre con motivo escrito.</summary>
    Adjustment = 5,

    /// <summary>Paso a otro camion en la calle.</summary>
    TransferOut = 6,

    /// <summary>Lo recibio de otro camion en la calle.</summary>
    TransferIn = 7
}
