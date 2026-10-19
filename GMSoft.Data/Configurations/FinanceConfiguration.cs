using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class ProductCostConfiguration : IEntityTypeConfiguration<ProductCost>
{
    public void Configure(EntityTypeBuilder<ProductCost> b)
    {
        b.ToTable("ProductCosts", t => t.HasCheckConstraint("CK_ProductCosts_UnitCost", "\"UnitCost\" >= 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.UnitCost).HasPrecision(18, 2);
        b.HasIndex(x => new { x.ProductId, x.ValidFrom }).IsUnique();
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}
public class DriverCommissionConfiguration : IEntityTypeConfiguration<DriverCommission>
{
    public void Configure(EntityTypeBuilder<DriverCommission> b)
    {
        b.ToTable("DriverCommissions", t => t.HasCheckConstraint("CK_DriverCommissions_Percent", "\"Percent\" >= 0 AND \"Percent\" <= 100"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Percent).HasPrecision(5, 2);
        b.HasIndex(x => new { x.DriverId, x.ValidFrom }).IsUnique();
        b.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
    }
}
public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> b)
    {
        b.ToTable("Expenses", t => t.HasCheckConstraint("CK_Expenses_Amount", "\"Amount\" > 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Category).HasConversion<int>();
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasIndex(x => x.Date);
        b.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
    }
}
