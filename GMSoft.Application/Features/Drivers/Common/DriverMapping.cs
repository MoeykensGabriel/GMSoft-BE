using GMSoft.Domain.Entities;

namespace GMSoft.Application.Features.Drivers.Common;

/// <summary>
/// Mapeo a mano y no por convencion: el DTO aplana datos del vehiculo, y el email
/// no vive en Driver sino en la cuenta de Identity, que Application no conoce.
/// </summary>
public static class DriverMapping
{
    public static DriverDto ToDto(Driver driver, string? email = null) => new(
        Id:                  driver.Id,
        FirstName:           driver.FirstName,
        LastName:            driver.LastName,
        DocumentNumber:      driver.DocumentNumber,
        Phone:               driver.Phone,
        VehicleId:           driver.VehicleId,
        VehicleName:         driver.Vehicle?.Name,
        VehicleLicensePlate: driver.Vehicle?.LicensePlate,
        Email:               email,
        IsActive:            driver.IsActive);
}
