using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class CustomerInspectionConfiguration : IEntityTypeConfiguration<CustomerInspection>
{
    public void Configure(EntityTypeBuilder<CustomerInspection> b)
    {
        b.ToTable("CustomerInspections");
        b.HasKey(i => i.Id);
        b.Property(i => i.NotBuyingReason).HasConversion<string>().HasMaxLength(30);
        b.Property(i => i.Notes).HasMaxLength(500);
        // Uno por cliente y salida: volver a controlar reemplaza el anterior.
        b.HasIndex(i => new { i.DeliverySessionId, i.CustomerId }).IsUnique();
        b.HasIndex(i => new { i.InspectorId, i.InspectedAt });
        b.HasOne(i => i.DeliverySession).WithMany().HasForeignKey(i => i.DeliverySessionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(i => i.Customer).WithMany().HasForeignKey(i => i.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(i => i.Inspector).WithMany().HasForeignKey(i => i.InspectorId).OnDelete(DeleteBehavior.Restrict);
        b.OwnsMany(i => i.Containers, owned =>
        {
            owned.ToTable("CustomerInspectionContainers");
            owned.WithOwner().HasForeignKey("CustomerInspectionId");
            owned.HasKey("CustomerInspectionId", nameof(InspectionContainerCount.ProductId));
        });
    }
}
