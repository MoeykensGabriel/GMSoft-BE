using FluentValidation;
using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Customers.Common;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Customers.Removal;

public record RequestRemoval(Guid CustomerId, Guid ClientRequestId, RemovalReason Reason,
    string? Notes, bool ContainersNotReturned) : IRequest<RemovalDto>;
public record ResolveRemoval(Guid RequestId, bool Confirm) : IRequest<RemovalDto>;
public record ListRemovals(RemovalStatus? Status = RemovalStatus.Pending, int Page = 1, int PageSize = 20)
    : IRequest<RemovalPage>;
public record RemovalPage(IReadOnlyList<RemovalDto> Items, int TotalCount, int PendingCount, int Page, int PageSize);
public record RemovalDto(Guid Id, Guid CustomerId, string CustomerName, Guid VehicleId, string VehicleName,
    Guid ZoneId, string ZoneName, Guid DriverId, string DriverName, Guid RequestedByUserId,
    DateTime RequestedAt, RemovalStatus Status, RemovalReason Reason, string? Notes,
    bool ContainersNotReturned, IReadOnlyList<RemovalContainerSnapshot> Containers, decimal Debt,
    Guid? ResolvedByUserId, DateTime? ResolvedAt, Guid ClientRequestId)
{
    public static RemovalDto From(CustomerRemovalRequest r) => new(r.Id, r.CustomerId,
        r.Customer.BusinessName ?? r.Customer.ContactName, r.VehicleId, r.Vehicle.Name,
        r.ZoneId, r.Zone.Name, r.DriverId, $"{r.Driver.FirstName} {r.Driver.LastName}",
        r.RequestedByUserId, r.RequestedAt, r.Status, r.Reason, r.Notes, r.ContainersNotReturned,
        r.Containers, r.Debt, r.ResolvedByUserId, r.ResolvedAt, r.ClientRequestId);
}
public class RequestRemovalValidator : AbstractValidator<RequestRemoval>
{
    public RequestRemovalValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.ClientRequestId).NotEmpty();
        RuleFor(x => x.Reason).IsInEnum();
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.Notes).Must(n => !string.IsNullOrWhiteSpace(n))
            .When(x => x.Reason == RemovalReason.Other).WithMessage("Otro requiere una nota.");
    }
}
public class ListRemovalsValidator : AbstractValidator<ListRemovals>
{
    public ListRemovalsValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 1000000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
    }
}
public class ContainersOutstandingException(IReadOnlyList<RemovalContainerSnapshot> containers)
    : ConflictException("El cliente tiene envases. Retiralos primero o marcá que no los devolvió.")
{
    public IReadOnlyList<RemovalContainerSnapshot> Containers { get; } = containers;
}
public interface ICustomerRemovalRepository
{
    Task LockAsync(CancellationToken ct);
    Task<CustomerRemovalRequest?> FindAsync(Guid id, bool byClientRequest, CancellationToken ct);
    Task<bool> HasPendingAsync(Guid customerId, CancellationToken ct);
    Task<List<RemovalContainerSnapshot>> ContainersAsync(Guid customerId, CancellationToken ct);
    Task AddAsync(CustomerRemovalRequest request, CancellationToken ct);
    Task<RemovalPage> ListAsync(ListRemovals query, CancellationToken ct);
}
public class RemovalHandlers(ICustomerRemovalRepository repository, ICustomerRepository customers,
    ISessionRepository sessions, CustomerRouteAccess access, ICurrentUserService user, IUnitOfWork uow)
    : IRequestHandler<RequestRemoval, RemovalDto>, IRequestHandler<ResolveRemoval, RemovalDto>,
      IRequestHandler<ListRemovals, RemovalPage>
{
    public async Task<RemovalDto> Handle(RequestRemoval command, CancellationToken ct)
    {
        if (!user.IsInRole(AppRoles.Driver) || user.DriverId is not Guid driver || user.UserId is not Guid actor)
            throw new ForbiddenException("Solo un chofer puede pedir la baja.");
        CustomerRemovalRequest? result = null;
        await uow.ExecuteInTransactionAsync(async () =>
        {
            await repository.LockAsync(ct);
            var previous = await repository.FindAsync(command.ClientRequestId, true, ct);
            if (previous is not null)
            {
                if (previous.RequestedByUserId != actor || previous.DriverId != driver)
                    throw new ForbiddenException("El envío pertenece a otro chofer.");
                if (previous.CustomerId != command.CustomerId || previous.Reason != command.Reason ||
                    previous.Notes != command.Notes?.Trim() || previous.ContainersNotReturned != command.ContainersNotReturned)
                    throw new ConflictException("El identificador ya fue usado con otros datos.");
                result = previous;
                return;
            }
            var session = await access.GetDriverSessionAsync(ct)
                ?? throw new ForbiddenException("Se requiere una salida de chofer.");
            if (!await sessions.LockOpenAsync(session.Id, ct)) throw new ConflictException("La salida ya fue recibida.");
            var customer = await customers.GetWithZoneAsync(command.CustomerId, ct)
                ?? throw new NotFoundException(nameof(Customer), command.CustomerId);
            try { CustomerRouteAccess.EnsureMatchesSession(customer, session); }
            catch (BadRequestException) { throw new ForbiddenException("El cliente no pertenece a tu recorrido."); }
            if (await repository.HasPendingAsync(customer.Id, ct)) throw new ConflictException("El cliente ya tiene una baja pendiente.");
            var containers = await repository.ContainersAsync(customer.Id, ct);
            if (containers.Count > 0 && !command.ContainersNotReturned) throw new ContainersOutstandingException(containers);
            result = new CustomerRemovalRequest { CustomerId = customer.Id, ClientRequestId = command.ClientRequestId,
                RequestedByUserId = actor, DriverId = driver, VehicleId = session.VehicleId, ZoneId = session.ZoneId,
                RequestedAt = DateTime.UtcNow, Reason = command.Reason, Notes = command.Notes?.Trim(),
                ContainersNotReturned = command.ContainersNotReturned, Containers = containers,
                Debt = await customers.GetAccountBalanceAsync(customer.Id, ct) };
            await repository.AddAsync(result, ct);
            await uow.SaveChangesAsync(ct);
            result = await repository.FindAsync(result.Id, false, ct);
        }, ct);
        return RemovalDto.From(result!);
    }
    public async Task<RemovalDto> Handle(ResolveRemoval command, CancellationToken ct)
    {
        if (!user.IsInRole(AppRoles.Admin) || user.UserId is not Guid actor) throw new ForbiddenException("Solo ADMIN resuelve bajas.");
        CustomerRemovalRequest? result = null;
        await uow.ExecuteInTransactionAsync(async () =>
        {
            await repository.LockAsync(ct);
            result = await repository.FindAsync(command.RequestId, false, ct)
                ?? throw new NotFoundException(nameof(CustomerRemovalRequest), command.RequestId);
            var status = command.Confirm ? RemovalStatus.Confirmed : RemovalStatus.Rejected;
            if (result.Status == status) return;
            if (result.Status != RemovalStatus.Pending) throw new ConflictException("La solicitud ya fue resuelta.");
            result.Status = status;
            result.ResolvedAt = DateTime.UtcNow;
            result.ResolvedByUserId = actor;
            if (command.Confirm) result.Customer.IsActive = false;
            await uow.SaveChangesAsync(ct);
        }, ct);
        return RemovalDto.From(result!);
    }
    public Task<RemovalPage> Handle(ListRemovals command, CancellationToken ct)
    {
        if (!user.IsInRole(AppRoles.Admin)) throw new ForbiddenException("Solo ADMIN consulta las bajas.");
        return repository.ListAsync(command, ct);
    }
}
