namespace GMSoft.Domain.Enums;

/// <summary>
/// Tipo de vehículo de la flota. Se persiste como int: los valores nuevos se
/// agregan al final para no correr los existentes.
/// </summary>
public enum VehicleType
{
    Motorcycle = 0,
    Car        = 1,
    Pickup     = 2,
    Van        = 3,
    Truck      = 4
}
