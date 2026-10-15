using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;

namespace GMSoft.Application.Features.Customers.Account;

public record StatementLine(Guid ProductId, string ProductDetail, int Quantity, decimal UnitPrice, decimal Amount);
public record StatementPayment(decimal Amount, PaymentMethod Method);
public record StatementContainerChange(Guid ProductId, string ProductDetail, int Quantity, string? Reason);
public record AccountStatementRow(DateTime Date, string Kind, Guid ReferenceId)
{
    public IReadOnlyList<StatementLine> Lines { get; init; } = [];
    public IReadOnlyList<CustomerContainerLineDto> ContainersDelivered { get; init; } = [];
    public IReadOnlyList<CustomerContainerLineDto> ContainersReturned { get; init; } = [];
    public IReadOnlyList<StatementContainerChange> ContainerChanges { get; init; } = [];
    public decimal Charge { get; init; }
    public IReadOnlyList<StatementPayment> Payments { get; init; } = [];
    public decimal Paid => Payments.Sum(p => p.Amount);
    public NoPurchaseReason? NoPurchaseReason { get; init; }
    public RemovalReason? RemovalReason { get; init; }
    public string? Notes { get; init; }
    public decimal BalanceAfter { get; init; }
}

public record AccountStatementResult(IReadOnlyList<AccountStatementRow> Rows, int TotalCount, decimal Balance);

public static class AccountStatementBuilder
{
    public static AccountStatementResult Build(IReadOnlyList<Delivery> deliveries,
        IReadOnlyList<Payment> payments, IReadOnlyList<ContainerMovement> adjustments,
        IReadOnlyList<Promotion> promotions, int limit, IReadOnlyList<CustomerRemovalRequest>? removals = null)
    {
        // No FK to Delivery exists. Only a unique exact match is evidence of a visit payment.
        var visits = deliveries.Where(d => d.Type != DeliveryType.NoPurchase)
            .GroupBy(d => (d.CustomerId, d.DeliverySessionId, d.DeliveredAt))
            .Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single().Id);
        var grouped = new Dictionary<Guid, List<StatementPayment>>();
        var rows = new List<AccountStatementRow>();
        foreach (var p in payments.OrderBy(p => p.PaidAt).ThenBy(p => p.Id))
        {
            var payment = new StatementPayment(p.Amount, p.Method);
            if (p.DeliverySessionId is Guid session && visits.TryGetValue((p.CustomerId, session, p.PaidAt), out var id))
            {
                if (!grouped.TryGetValue(id, out var list)) grouped[id] = list = [];
                list.Add(payment);
            }
            else rows.Add(new(p.PaidAt, "Payment", p.Id) { Payments = [payment], Notes = p.Notes });
        }
        foreach (var d in deliveries)
            rows.Add(new(d.DeliveredAt, d.Type.ToString(), d.Id)
            {
                Lines = d.Items.OrderBy(i => i.ProductId).Select(i => new StatementLine(i.ProductId,
                    i.Product.Detail, i.Quantity, i.UnitPrice, i.Quantity * i.UnitPrice)).ToList(),
                ContainersDelivered = Containers(d.ContainerMovements, ContainerMovementType.DeliveredToCustomer),
                ContainersReturned = Containers(d.ContainerMovements, ContainerMovementType.ReturnedFromCustomer),
                Charge = d.Type == DeliveryType.NoPurchase ? 0 : d.Total,
                Payments = grouped.GetValueOrDefault(d.Id) ?? [],
                NoPurchaseReason = d.NoPurchaseReason, Notes = d.Notes
            });
        foreach (var m in adjustments)
            rows.Add(new(m.OccurredAt, m.Type == ContainerMovementType.Lost ? "ContainerLoss" : "ContainerAdjustment", m.Id)
            {
                ContainerChanges = [new(m.ProductId, m.Product.Detail, m.Quantity, m.Notes)], Notes = m.Notes
            });
        foreach (var p in promotions)
            rows.Add(new(p.RegisteredAt, "Promotion", p.Id)
            {
                Lines = p.Lines.OrderBy(l => l.ProductId).Select(l => new StatementLine(l.ProductId,
                    l.Product.Detail, l.Quantity, 0, 0)).ToList(),
                ContainersDelivered = p.Lines.Where(l => l.ContainersLoaned > 0).OrderBy(l => l.ProductId)
                    .Select(l => new CustomerContainerLineDto(l.ProductId, l.Product.Detail, l.ContainersLoaned)).ToList(),
                Notes = p.Notes
            });
        foreach (var r in removals ?? [])
        {
            rows.Add(new(r.RequestedAt, "RemovalRequested", r.Id) { RemovalReason = r.Reason, Notes = r.Notes });
            if (r.ResolvedAt is DateTime resolved)
                rows.Add(new(resolved, r.Status == RemovalStatus.Confirmed ? "RemovalConfirmed" : "RemovalRejected", r.Id)
                    { RemovalReason = r.Reason, Notes = r.Notes });
        }
        decimal balance = 0;
        var chronological = rows.OrderBy(r => r.Date).ThenBy(r => r.Kind, StringComparer.Ordinal).ThenBy(r => r.ReferenceId)
            .Select(r => r with { BalanceAfter = balance += r.Charge - r.Paid }).ToList();
        chronological.Reverse();
        return new(chronological.Take(limit).ToList(), rows.Count, balance);
    }

    private static IReadOnlyList<CustomerContainerLineDto> Containers(IEnumerable<ContainerMovement> movements, ContainerMovementType type)
        => movements.Where(m => m.Type == type).GroupBy(m => new { m.ProductId, m.Product.Detail })
            .OrderBy(g => g.Key.ProductId).Select(g => new CustomerContainerLineDto(g.Key.ProductId,
                g.Key.Detail, Math.Abs(g.Sum(m => m.Quantity)))).ToList();
}
