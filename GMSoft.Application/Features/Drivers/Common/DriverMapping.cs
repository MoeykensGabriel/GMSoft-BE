using GMSoft.Domain.Entities;

namespace GMSoft.Application.Features.Drivers.Common;

/// <summary>
/// Mapeo a mano y no por convencion: el DTO aplana datos del vehiculo, y el usuario
/// y el email no viven en Driver sino en la cuenta de Identity, que Application no
/// conoce. Los trae quien llama, resueltos en una sola consulta.
/// </summary>
public static class DriverMapping
{
    public static DriverDto ToDto(Driver driver, string? userName = null, string? email = null) => new(
        Id:                  driver.Id,
        FirstName:           driver.FirstName,
        LastName:            driver.LastName,
        DocumentNumber:      driver.DocumentNumber,
        Phone:               driver.Phone,
        VehicleId:           driver.VehicleId,
        VehicleName:         driver.Vehicle?.Name,
        VehicleLicensePlate: driver.Vehicle?.LicensePlate,
        UserName:            userName,
        Email:               email,
        IsActive:            driver.IsActive);
}
