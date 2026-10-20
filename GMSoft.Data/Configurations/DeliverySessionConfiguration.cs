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
        builder.Property(s => s.RouteDays).HasColumnType("integer[]");

        builder.Property(s => s.Status).HasConversion<int>();
        builder.Property(s => s.CancellationReason).HasMaxLength(300);

        // Reemplaza al viejo Vehicle.HasOpenSession: la pregunta "tiene sesion
        // abierta este vehiculo" se responde con este indice.
        builder.HasIndex(s => new { s.VehicleId, s.Status });
        builder.HasIndex(s => new { s.DriverId, s.Status });
        // SessionStatus.Open = 0. La base protege tambien escrituras fuera del handler.
        builder.HasIndex(s => s.VehicleId).IsUnique().HasFilter("\"Status\" = 0")
            .HasDatabaseName("IX_DeliverySessions_OpenVehicle");
        builder.HasIndex(s => s.DriverId).IsUnique().HasFilter("\"Status\" = 0")
            .HasDatabaseName("IX_DeliverySessions_OpenDriver");

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
