using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class VehicleKilometersReadingConfiguration : IEntityTypeConfiguration<VehicleKilometersReading>
{
    public void Configure(EntityTypeBuilder<VehicleKilometersReading> builder)
    {
        builder.ToTable("VehicleKilometersReadings");
        builder.HasKey(r => r.Id);
        builder.HasIndex(r => new { r.VehicleId, r.Status });
        builder.HasOne(r => r.Vehicle).WithMany().HasForeignKey(r => r.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.DeliverySession).WithMany().HasForeignKey(r => r.DeliverySessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
