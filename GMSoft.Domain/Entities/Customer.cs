using GMSoft.Domain.Common;

namespace GMSoft.Domain.Entities;

/// <summary>
/// Cliente al que se le reparte. La direccion es un atributo suyo: un cliente,
/// un punto de entrega.
/// </summary>
public class Customer : BaseEntity
{
    /// <summary>
    /// Razon social, para los clientes comerciales. En una casa de familia va nulo:
    /// no todo cliente es un comercio.
    /// </summary>
    public string? BusinessName { get; set; }

    /// <summary>
    /// Persona con la que se trata y que recibe la entrega. Siempre esta, sea un
    /// comercio o una casa. Para mostrar el cliente se usa la razon social si existe
    /// y este nombre si no.
    /// </summary>
    public string ContactName { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? Email { get; set; }

    /// <summary>
    /// Posicion en el recorrido. El orden del reparto es el orden en que se fueron
    /// cargando los clientes, asi que al dar de alta se asigna el siguiente numero
    /// libre. Es un campo y no la fecha de creacion porque asi se puede reordenar:
    /// con la fecha no hay forma de meter un cliente nuevo entre el cuarto y el quinto.
    /// </summary>
    public int RouteOrder { get; set; }

    /// <summary>Indicaciones para el chofer: timbre, horarios, referencias.</summary>
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Precios particulares. Sin fila para un producto, vale el precio del catalogo.</summary>
    public ICollection<CustomerProductPrice> Prices { get; set; } = new List<CustomerProductPrice>();
}
