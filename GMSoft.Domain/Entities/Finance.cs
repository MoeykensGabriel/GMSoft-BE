using GMSoft.Domain.Common;
using GMSoft.Domain.Enums;

namespace GMSoft.Domain.Entities;

public class ProductCost : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public decimal UnitCost { get; set; }
    public DateOnly ValidFrom { get; set; }
}

public class DriverCommission : BaseEntity
{
    public Guid DriverId { get; set; }
    public Driver Driver { get; set; } = null!;
    public decimal Percent { get; set; }
    public DateOnly ValidFrom { get; set; }
}

public class Expense : BaseEntity
{
    public DateOnly Date { get; set; }
    public ExpenseCategory Category { get; set; }
    public decimal Amount { get; set; }
    public Guid? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    public string? Description { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
