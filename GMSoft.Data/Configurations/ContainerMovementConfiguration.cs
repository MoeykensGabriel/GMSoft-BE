using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class ContainerMovementConfiguration : IEntityTypeConfiguration<ContainerMovement>
{
    public void Configure(EntityTypeBuilder<ContainerMovement> builder)
    {
        builder.ToTable("ContainerMovements");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Type).HasConversion<int>();
        builder.Property(m => m.Notes).HasMaxLength(500);

        // Reconstruir el saldo de un cliente recorre sus movimientos por producto.
        builder.HasIndex(m => new { m.CustomerId, m.ProductId, m.OccurredAt });

        // Nada de Cascade en el libro mayor: es el registro que explica los saldos
        // y no puede desaparecer porque se borre algo de alrededor.
        builder.HasOne(m => m.Product)
               .WithMany()
               .HasForeignKey(m => m.ProductId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Customer)
               .WithMany()
               .HasForeignKey(m => m.CustomerId)
               .OnDelete(DeleteBehavior.Restrict);

        // Un movimiento por visita, producto y direccion. Sin esto, guardar dos
        // veces la misma visita duplica los envases del cliente en silencio.
        builder.HasIndex(m => new { m.DeliveryId, m.ProductId, m.Type }).IsUnique();

        builder.HasOne(m => m.Delivery)
               .WithMany(d => d.ContainerMovements)
               .HasForeignKey(m => m.DeliveryId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.ContainerUnit)
               .WithMany()
               .HasForeignKey(m => m.ContainerUnitId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
