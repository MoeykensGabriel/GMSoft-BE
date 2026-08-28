using MediatR;

namespace GMSoft.Application.Features.Drivers.ResetPassword;

/// <summary>El admin le pone una contraseña nueva. No pide la anterior.</summary>
public record ResetDriverPasswordCommand(Guid Id, string NewPassword) : IRequest;
