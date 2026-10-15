using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class CustomerRemovalConfiguration : IEntityTypeConfiguration<CustomerRemovalRequest>
{
    public void Configure(EntityTypeBuilder<CustomerRemovalRequest> b)
    {
        b.ToTable("CustomerRemovalRequests");
        b.HasKey(r => r.Id);
        b.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(r => r.Reason).HasConversion<string>().HasMaxLength(30);
        b.Property(r => r.Notes).HasMaxLength(1000);
        b.Property(r => r.Debt).HasPrecision(18, 2);
        b.HasIndex(r => r.ClientRequestId).IsUnique();
        b.HasIndex(r => r.CustomerId).IsUnique().HasFilter("\"Status\" = 'Pending'");
        b.HasIndex(r => new { r.Status, r.RequestedAt });
        b.HasOne(r => r.Customer).WithMany(c => c.RemovalRequests).HasForeignKey(r => r.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(r => r.Driver).WithMany().HasForeignKey(r => r.DriverId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(r => r.Vehicle).WithMany().HasForeignKey(r => r.VehicleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(r => r.Zone).WithMany().HasForeignKey(r => r.ZoneId).OnDelete(DeleteBehavior.Restrict);
        b.OwnsMany(r => r.Containers, owned =>
        {
            owned.ToTable("CustomerRemovalContainers");
            owned.WithOwner().HasForeignKey("RemovalRequestId");
            owned.HasKey("RemovalRequestId", nameof(RemovalContainerSnapshot.ProductId));
            owned.Property(c => c.ProductDetail).HasMaxLength(300);
        });
    }
}
