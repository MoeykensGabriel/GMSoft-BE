using GMSoft.Application.Common.Models;
using MediatR;

namespace GMSoft.Application.Features.Auth.Login;

public record LoginCommand(string UserName, string Password) : IRequest<AuthResult>;
