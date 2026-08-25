using GMSoft.Domain.Common;

namespace GMSoft.Domain.Entities;

/// <summary>
/// Chofer que hace el reparto. Entra al sistema con su propia cuenta: abre y
/// cierra la sesión y carga las entregas en el momento.
/// </summary>
public class Driver : BaseEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// Vehículo asignado. Varios choferes pueden tener asignado el mismo vehículo,
    /// así que el FK vive de este lado y no lleva índice único.
    /// </summary>
    public Guid? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    /// <summary>
    /// Cuenta de Identity con la que entra al sistema. Solo el Guid, sin navegación:
    /// el Domain no puede depender de Identity.
    /// </summary>
    public Guid? ApplicationUserId { get; set; }

    public bool IsActive { get; set; } = true;
}
