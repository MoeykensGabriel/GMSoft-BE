using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Name).IsRequired().HasMaxLength(100);
        builder.Property(v => v.LicensePlate).IsRequired().HasMaxLength(15);
        builder.Property(v => v.Type).HasConversion<int>();

        // La patente identifica al vehiculo: no puede repetirse.
        builder.HasIndex(v => v.LicensePlate).IsUnique();
    }
}
