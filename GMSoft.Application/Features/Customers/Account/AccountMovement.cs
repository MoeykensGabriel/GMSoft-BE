namespace GMSoft.Application.Features.Customers.Account;

public enum AccountMovementType
{
    /// <summary>Una visita con venta. Suma a lo que debe.</summary>
    Delivery = 0,

    /// <summary>Un cobro. Resta.</summary>
    Payment = 1
}

/// <summary>Una linea del resumen de cuenta.</summary>
public record AccountMovement(
    DateTime            Date,
    AccountMovementType Type,
    decimal             Amount,
    Guid                ReferenceId,
    string?             Notes);
