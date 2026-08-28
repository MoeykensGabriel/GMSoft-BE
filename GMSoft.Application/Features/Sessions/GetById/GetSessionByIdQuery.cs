using GMSoft.Application.Features.Sessions.Common;
using MediatR;

namespace GMSoft.Application.Features.Sessions.GetById;

public record GetSessionByIdQuery(Guid Id) : IRequest<SessionDto>;
