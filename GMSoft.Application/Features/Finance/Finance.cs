using FluentValidation;
using GMSoft.Application.Common;
using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.Finance;

public record CostVersion(DateOnly ValidFrom, decimal UnitCost);
public record ProductCostsDto(Guid ProductId, string ProductDetail, decimal? CurrentUnitCost, IReadOnlyList<CostVersion> History);
public record CommissionVersion(DateOnly ValidFrom, decimal Percent);
public record DriverCommissionsDto(Guid DriverId, string DriverName, decimal? CurrentPercent, IReadOnlyList<CommissionVersion> History);
public record ExpenseDto(Guid Id, DateOnly Date, ExpenseCategory Category, decimal Amount, Guid? VehicleId,
    string? VehicleName, string? Description, Guid? CreatedByUserId);
public record GetCostsQuery : IRequest<IReadOnlyList<ProductCostsDto>>;
public record GetCommissionsQuery : IRequest<IReadOnlyList<DriverCommissionsDto>>;
public record PutCostCommand(Guid ProductId, decimal UnitCost, DateOnly ValidFrom) : IRequest;
public record PutCommissionCommand(Guid DriverId, decimal Percent, DateOnly ValidFrom) : IRequest;
public record GetExpensesQuery(int Year, int Month) : IRequest<IReadOnlyList<ExpenseDto>>;
public record SaveExpenseCommand(Guid? Id, DateOnly Date, ExpenseCategory Category, decimal Amount,
    Guid? VehicleId, string? Description) : IRequest<ExpenseDto>;
public record DeleteExpenseCommand(Guid Id) : IRequest;

public interface IFinanceRepository
{
    Task<IReadOnlyList<ProductCostsDto>> GetCostsAsync(DateOnly today, CancellationToken ct);
    Task<IReadOnlyList<DriverCommissionsDto>> GetCommissionsAsync(DateOnly today, CancellationToken ct);
    Task PutCostAsync(PutCostCommand request, CancellationToken ct);
    Task PutCommissionAsync(PutCommissionCommand request, CancellationToken ct);
    Task<IReadOnlyList<ExpenseDto>> GetExpensesAsync(DateOnly from, DateOnly to, CancellationToken ct);
    Task<ExpenseDto> SaveExpenseAsync(SaveExpenseCommand request, Guid? userId, CancellationToken ct);
    Task DeleteExpenseAsync(Guid id, CancellationToken ct);
    Task<ProfitData> GetProfitDataAsync(DateTime from, DateTime to, CancellationToken ct);
}

public class PutCostCommandValidator : AbstractValidator<PutCostCommand>
{
    public PutCostCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, true);
        RuleFor(x => x.ValidFrom).GreaterThan(DateOnly.MinValue).LessThan(DateOnly.MaxValue);
    }
}
public class PutCommissionCommandValidator : AbstractValidator<PutCommissionCommand>
{
    public PutCommissionCommandValidator()
    {
        RuleFor(x => x.DriverId).NotEmpty();
        RuleFor(x => x.Percent).InclusiveBetween(0, 100).PrecisionScale(5, 2, true);
        RuleFor(x => x.ValidFrom).GreaterThan(DateOnly.MinValue).LessThan(DateOnly.MaxValue);
    }
}
public class GetExpensesQueryValidator : AbstractValidator<GetExpensesQuery>
{
    public GetExpensesQueryValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2020, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}
public class SaveExpenseCommandValidator : AbstractValidator<SaveExpenseCommand>
{
    public SaveExpenseCommandValidator()
    {
        RuleFor(x => x.Id).NotEqual(Guid.Empty);
        RuleFor(x => x.Date).GreaterThan(DateOnly.MinValue).LessThan(DateOnly.MaxValue);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Amount).GreaterThan(0).PrecisionScale(18, 2, true);
        RuleFor(x => x.VehicleId).NotEqual(Guid.Empty);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
public class DeleteExpenseCommandValidator : AbstractValidator<DeleteExpenseCommand>
{
    public DeleteExpenseCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public class FinanceHandlers(IFinanceRepository repository, ICurrentUserService user, TimeProvider clock)
    : IRequestHandler<GetCostsQuery, IReadOnlyList<ProductCostsDto>>,
      IRequestHandler<GetCommissionsQuery, IReadOnlyList<DriverCommissionsDto>>,
      IRequestHandler<PutCostCommand>, IRequestHandler<PutCommissionCommand>,
      IRequestHandler<GetExpensesQuery, IReadOnlyList<ExpenseDto>>,
      IRequestHandler<SaveExpenseCommand, ExpenseDto>, IRequestHandler<DeleteExpenseCommand>
{
    private void RequireAdmin()
    {
        if (!user.IsInRole(AppRoles.Admin)) throw new ForbiddenException("Las finanzas las maneja la oficina.");
    }
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.Add(BusinessTime.Offset));
    public Task<IReadOnlyList<ProductCostsDto>> Handle(GetCostsQuery request, CancellationToken ct)
    {
        RequireAdmin();
        return repository.GetCostsAsync(Today, ct);
    }
    public Task<IReadOnlyList<DriverCommissionsDto>> Handle(GetCommissionsQuery request, CancellationToken ct)
    {
        RequireAdmin();
        return repository.GetCommissionsAsync(Today, ct);
    }
    public Task Handle(PutCostCommand request, CancellationToken ct)
    {
        RequireAdmin();
        return repository.PutCostAsync(request, ct);
    }
    public Task Handle(PutCommissionCommand request, CancellationToken ct)
    {
        RequireAdmin();
        return repository.PutCommissionAsync(request, ct);
    }
    public Task<IReadOnlyList<ExpenseDto>> Handle(GetExpensesQuery request, CancellationToken ct)
    {
        RequireAdmin();
        var first = new DateOnly(request.Year, request.Month, 1);
        return repository.GetExpensesAsync(first, first.AddMonths(1), ct);
    }
    public Task<ExpenseDto> Handle(SaveExpenseCommand request, CancellationToken ct)
    {
        RequireAdmin();
        return repository.SaveExpenseAsync(request, user.UserId, ct);
    }
    public Task Handle(DeleteExpenseCommand request, CancellationToken ct)
    {
        RequireAdmin();
        return repository.DeleteExpenseAsync(request.Id, ct);
    }
}
