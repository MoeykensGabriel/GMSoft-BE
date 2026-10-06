using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.BusinessName).HasMaxLength(200);
        builder.Property(c => c.ContactName).IsRequired().HasMaxLength(150);
        builder.Property(c => c.Phone).IsRequired().HasMaxLength(30);
        builder.Property(c => c.Address).IsRequired().HasMaxLength(300);
        builder.Property(c => c.Email).HasMaxLength(150);
        builder.Property(c => c.Notes).HasMaxLength(1000);
        builder.Property(c => c.VisitDays).HasColumnType("integer[]");

        // El recorrido de una zona va en el orden en que se cargaron sus clientes.
        // Es la consulta que arma la hoja de ruta del chofer al abrir la sesion.
        builder.HasIndex(c => new { c.ZoneId, c.RouteOrder });
        builder.HasIndex(c => new { c.VehicleId, c.ZoneId, c.RouteOrder });
        builder.HasOne(c => c.Vehicle).WithMany().HasForeignKey(c => c.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        // Restrict: una zona con clientes no se da de baja y los deja sin recorrido.
        builder.HasOne(c => c.Zone)
               .WithMany(z => z.Customers)
               .HasForeignKey(c => c.ZoneId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
