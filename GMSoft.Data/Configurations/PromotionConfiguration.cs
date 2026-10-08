using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> b)
    {
        b.ToTable("Promotions");
        b.HasKey(p => p.Id);
        b.Property(p => p.BusinessName).HasMaxLength(200);
        b.Property(p => p.ContactName).HasMaxLength(150).IsRequired();
        b.Property(p => p.Phone).HasMaxLength(30).IsRequired();
        b.Property(p => p.Address).HasMaxLength(300).IsRequired();
        b.Property(p => p.Notes).HasMaxLength(1000);
        b.Property(p => p.Status).HasConversion<int>().IsConcurrencyToken();
        b.HasIndex(p => p.ClientRequestId).IsUnique();
        b.HasIndex(p => p.CloseClientRequestId).IsUnique();
        b.HasIndex(p => new { p.VehicleId, p.Status, p.PickupDate });
        b.HasIndex(p => p.RegisteredAt);
        b.HasOne(p => p.Vehicle).WithMany().HasForeignKey(p => p.VehicleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p => p.Driver).WithMany().HasForeignKey(p => p.DriverId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p => p.DeliverySession).WithMany().HasForeignKey(p => p.DeliverySessionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p => p.Zone).WithMany().HasForeignKey(p => p.ZoneId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p => p.Customer).WithMany().HasForeignKey(p => p.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Driver>().WithMany().HasForeignKey(p => p.ClosedByDriverId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DeliverySession>().WithMany().HasForeignKey(p => p.ClosingSessionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PromotionLineConfiguration : IEntityTypeConfiguration<PromotionLine>
{
    public void Configure(EntityTypeBuilder<PromotionLine> b)
    {
        b.ToTable("PromotionLines", t => t.HasCheckConstraint("CK_PromotionLines_Quantities",
            "\"Quantity\" > 0 AND \"ContainersLoaned\" >= 0 AND \"ContainersLoaned\" <= \"Quantity\" AND \"ContainersReturned\" >= 0 AND \"ContainersLost\" >= 0 AND \"ContainersReturned\" + \"ContainersLost\" <= \"ContainersLoaned\""));
        b.HasKey(p => p.Id);
        b.HasIndex(p => new { p.PromotionId, p.ProductId }).IsUnique();
        b.HasOne(p => p.Promotion).WithMany(p => p.Lines).HasForeignKey(p => p.PromotionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p => p.Product).WithMany().HasForeignKey(p => p.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PromotionContainerMovementConfiguration : IEntityTypeConfiguration<PromotionContainerMovement>
{
    public void Configure(EntityTypeBuilder<PromotionContainerMovement> b)
    {
        b.ToTable("PromotionContainerMovements");
        b.HasKey(p => p.Id);
        b.HasOne(p => p.Promotion).WithMany(p => p.ContainerMovements).HasForeignKey(p => p.PromotionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p => p.ContainerMovement).WithMany().HasForeignKey(p => p.ContainerMovementId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(p => p.ContainerMovementId).IsUnique();
    }
}

public class PromotionSettingsConfiguration : IEntityTypeConfiguration<PromotionSettings>
{
    public void Configure(EntityTypeBuilder<PromotionSettings> b)
    {
        b.ToTable("PromotionSettings", t =>
        {
            t.HasCheckConstraint("CK_PromotionSettings_Singleton", "\"Id\" = 1");
            t.HasCheckConstraint("CK_PromotionSettings_PickupDays", "\"PickupDays\" > 0");
        });
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).ValueGeneratedNever();
        b.Property(p => p.PickupDays).HasDefaultValue(7);
    }
}

