using GMSoft.Application.Common.Models;
using MediatR;

namespace GMSoft.Application.Features.Auth.Login;

public record LoginCommand(string Email, string Password) : IRequest<AuthResult>;
