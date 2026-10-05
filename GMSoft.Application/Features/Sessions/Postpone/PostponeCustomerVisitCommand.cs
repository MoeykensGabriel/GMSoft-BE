using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Sessions.Postpone;

public record PostponeCustomerVisitCommand(Guid CustomerId) : IRequest;

public class PostponeCustomerVisitCommandHandler(
    ISessionRepository sessions, ICustomerRepository customers,
    ICurrentUserService user, IUnitOfWork work) : IRequestHandler<PostponeCustomerVisitCommand>
{
    public async Task Handle(PostponeCustomerVisitCommand request, CancellationToken cancellationToken)
    {
        var driverId = user.DriverId ?? throw new ForbiddenException("Solo el chofer puede posponer una visita de su recorrido.");
        var session = await sessions.GetOpenByDriverAsync(driverId, cancellationToken)
            ?? throw new ConflictException("No tenés una salida abierta.");
        var customer = await customers.GetByIdAsync(request.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.CustomerId);
        if (!customer.IsActive || customer.ZoneId != session.ZoneId)
            throw new BadRequestException("El cliente no está activo en la zona de esta salida.");
        var deferred = session.DeferredCustomerIds ?? [];
        if (deferred.Contains(customer.Id)) return;
        session.DeferredCustomerIds = [.. deferred, customer.Id];
        sessions.Update(session);
        await work.SaveChangesAsync(cancellationToken);
    }
}
