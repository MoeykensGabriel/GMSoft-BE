namespace GMSoft.Domain.Enums;

/// <summary>Por qué se movió un envase.</summary>
public enum ContainerMovementType
{
    /// <summary>Salió con el cliente.</summary>
    DeliveredToCustomer = 0,

    /// <summary>El cliente lo devolvió.</summary>
    ReturnedFromCustomer = 1,

    /// <summary>Corrección manual del saldo. Siempre con motivo escrito.</summary>
    Adjustment = 2,

    /// <summary>Se dio por perdido o roto.</summary>
    Lost = 3
}
