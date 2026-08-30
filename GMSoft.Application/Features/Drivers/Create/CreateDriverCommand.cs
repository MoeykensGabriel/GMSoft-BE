using MediatR;

namespace GMSoft.Application.Features.Drivers.Create;

/// <summary>
/// Alta de chofer. El admin le asigna el usuario y la contraseña en el mismo acto,
/// asi que crea la cuenta y la ficha juntas. El email es opcional: es contacto, no
/// credencial, y el chofer puede no tener.
/// </summary>
public record CreateDriverCommand(
    string  FirstName,
    string  LastName,
    string  DocumentNumber,
    string  Phone,
    string  UserName,
    string  Password,
    string? Email,
    Guid?   VehicleId) : IRequest<Guid>;
