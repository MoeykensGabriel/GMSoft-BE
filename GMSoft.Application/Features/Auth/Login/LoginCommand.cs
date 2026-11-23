using GMSoft.Application.Common.Models;
using MediatR;

namespace GMSoft.Application.Features.Auth.Login;

/// <summary>
/// <c>Installed</c>: la app está abierta desde el ícono instalado. <c>DeviceId</c>: el
/// identificador que la app guarda en el celular. Los dos sirven al control de sesiones.
/// </summary>
public record LoginCommand(string UserName, string Password, bool? Installed = null, Guid? DeviceId = null)
    : IRequest<AuthResult>;
