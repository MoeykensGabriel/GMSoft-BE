using GMSoft.Domain.Common;

namespace GMSoft.Domain.Entities;

/// <summary>
/// Cliente al que se le reparte. La dirección es un atributo suyo: un cliente,
/// un punto de entrega. El reparto del día se ordena por ese campo.
/// </summary>
public class Customer : BaseEntity
{
    /// <summary>
    /// Razón social, para los clientes comerciales. En una casa de familia va nulo:
    /// no todo cliente es un comercio.
    /// </summary>
    public string? BusinessName { get; set; }

    /// <summary>
    /// Persona con la que se trata y que recibe la entrega. Siempre está, sea un
    /// comercio o una casa. Para mostrar el cliente se usa la razón social si existe
    /// y este nombre si no.
    /// </summary>
    public string ContactName { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? Email { get; set; }

    /// <summary>Indicaciones para el chofer: timbre, horarios, referencias.</summary>
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;
}
