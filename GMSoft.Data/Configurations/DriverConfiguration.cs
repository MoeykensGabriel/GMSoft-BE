using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.ToTable("Drivers");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(d => d.LastName).IsRequired().HasMaxLength(100);
        builder.Property(d => d.DocumentNumber).IsRequired().HasMaxLength(50);
        builder.Property(d => d.Phone).IsRequired().HasMaxLength(30);

        builder.HasIndex(d => d.DocumentNumber).IsUnique();

        // Se consulta en cada login para resolver el DriverId del token. Unico
        // porque una cuenta es de un solo chofer; en Postgres los nulos no chocan
        // entre si, asi que los usuarios que no son choferes no molestan.
        builder.HasIndex(d => d.ApplicationUserId).IsUnique();

        // Varios choferes pueden compartir vehiculo, por eso no lleva unico.
        // Si se da de baja el vehiculo, el chofer queda sin asignacion, no se borra.
        builder.HasOne(d => d.Vehicle)
               .WithMany(v => v.Drivers)
               .HasForeignKey(d => d.VehicleId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
