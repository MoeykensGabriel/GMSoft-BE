using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class RouteOrderChangeConfiguration : IEntityTypeConfiguration<RouteOrderChange>
{
    public void Configure(EntityTypeBuilder<RouteOrderChange> builder)
    {
        builder.HasKey(c => c.Id);
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(c => c.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Zone>().WithMany().HasForeignKey(c => c.ZoneId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DeliverySession>().WithMany().HasForeignKey(c => c.DeliverySessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.VehicleId, c.ZoneId, c.ChangedAt });
        builder.HasIndex(c => c.DeliverySessionId);
    }
}
