using MediatR;

namespace GMSoft.Application.Features.Drivers.Update;

/// <summary>Datos de la ficha y asignacion de vehiculo. La contraseña va aparte.</summary>
public record UpdateDriverCommand(
    Guid   Id,
    string FirstName,
    string LastName,
    string DocumentNumber,
    string Phone,
    Guid?  VehicleId,
    bool   IsActive) : IRequest;
