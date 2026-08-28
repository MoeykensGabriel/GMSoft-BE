using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class DeliverySessionConfiguration : IEntityTypeConfiguration<DeliverySession>
{
    public void Configure(EntityTypeBuilder<DeliverySession> builder)
    {
        builder.ToTable("DeliverySessions");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Status).HasConversion<int>();

        // Reemplaza al viejo Vehicle.HasOpenSession: la pregunta "tiene sesion
        // abierta este vehiculo" se responde con este indice.
        builder.HasIndex(s => new { s.VehicleId, s.Status });
        builder.HasIndex(s => new { s.DriverId, s.Status });

        // Restrict y no Cascade: dar de baja un chofer o un vehiculo no puede
        // llevarse puesto el historial de reparto.
        builder.HasOne(s => s.Zone)
               .WithMany()
               .HasForeignKey(s => s.ZoneId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Driver)
               .WithMany()
               .HasForeignKey(s => s.DriverId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Vehicle)
               .WithMany(v => v.Sessions)
               .HasForeignKey(s => s.VehicleId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
