using GMSoft.Domain.Enums;

namespace GMSoft.Application.Features.Reports.Common;

/// <summary>
/// Una linea del reporte de envases en la calle. Es la pregunta que justifica el
/// sistema: cuantos envases tuyos estan afuera y en manos de cuanta gente.
/// </summary>
public record ContainersOutLineDto(
    Guid              ProductId,
    string            ProductDetail,
    ContainerTracking Tracking,

    /// <summary>Total en poder de clientes. Por saldo o por unidades, segun el producto.</summary>
    int               QuantityOut,

    /// <summary>Entre cuantos clientes estan repartidos.</summary>
    int               CustomersHolding);

/// <summary>
/// Un cliente que debe plata. Trae tambien los envases: quien debe y ademas tiene
/// envases tuyos es un problema mas grande que quien solo debe.
/// </summary>
public record DebtorLineDto(
    Guid      CustomerId,
    string    DisplayName,
    string    Phone,
    string    Address,
    string?   ZoneName,
    decimal   Balance,
    DateTime? LastPurchaseAt,
    int?      DaysWithoutPurchase,
    int       ContainersHeld);

/// <summary>
/// Un cliente que dejo de comprar. Lo que importa no es solo que se fue, sino si se
/// fue con envases tuyos y debiendo plata.
/// </summary>
public record InactiveCustomerLineDto(
    Guid      CustomerId,
    string    DisplayName,
    string    Phone,
    string    Address,
    string?   ZoneName,
    DateTime? LastPurchaseAt,
    int?      DaysWithoutPurchase,
    decimal   Balance,
    int       ContainersHeld);
