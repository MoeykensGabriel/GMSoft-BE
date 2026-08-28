using GMSoft.Domain.Common;

namespace GMSoft.Domain.Entities;

/// <summary>
/// Zona de reparto: Palermo, Lomas de Zamora. Es la unidad de trabajo de una
/// salida. El chofer elige la zona al abrir la sesion y el recorrido son los
/// clientes de esa zona, en su orden.
/// </summary>
public class Zone : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Los clientes que se reparten en esta zona.</summary>
    public ICollection<Customer> Customers { get; set; } = new List<Customer>();
}
