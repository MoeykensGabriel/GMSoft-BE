namespace GMSoft.Application.Features.Customers.Account;

/// <summary>
/// Lo que el negocio necesita saber de un cliente parado en su puerta: cuanto debe
/// y cuantos envases tuyos tiene.
/// </summary>
public record CustomerAccountDto(
    Guid      CustomerId,
    string    DisplayName,
    string    Address,
    string    Phone,
    string?   ZoneName,

    /// <summary>Suma de entregas menos suma de pagos. Positivo es deuda del cliente.</summary>
    decimal   Balance,

    DateTime? LastPurchaseAt,
    int?      DaysWithoutPurchase,

    /// <summary>
    /// Envases en su poder contados por cantidad, para los productos que se siguen
    /// por saldo. Es el activo que hay que recuperar.
    /// </summary>
    IReadOnlyList<CustomerContainerLineDto> Containers,

    /// <summary>
    /// Unidades identificadas en su poder, con su numero de serie. Para los envases
    /// seguidos por unidad no se lleva saldo por cantidad: cuantas tiene se cuenta
    /// desde aca, y asi no hay dos numeros para lo mismo.
    /// </summary>
    IReadOnlyList<CustomerUnitLineDto> Units,

    /// <summary>
    /// Ultimos movimientos, del mas nuevo al mas viejo. No llevan saldo acumulado
    /// por linea a proposito: sobre una lista recortada ese numero seria falso.
    /// El saldo bueno es Balance, que suma todo.
    /// </summary>
    IReadOnlyList<AccountMovement> Movements);

public record CustomerContainerLineDto(
    Guid   ProductId,
    string ProductDetail,
    int    Quantity);

public record CustomerUnitLineDto(
    Guid   ContainerUnitId,
    Guid   ProductId,
    string ProductDetail,
    string SerialNumber);
