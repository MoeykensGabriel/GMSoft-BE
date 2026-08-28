using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class DeliveryContainerReturnConfiguration : IEntityTypeConfiguration<DeliveryContainerReturn>
{
    public void Configure(EntityTypeBuilder<DeliveryContainerReturn> builder)
    {
        builder.ToTable("DeliveryContainerReturns");
        builder.HasKey(r => r.Id);

        // Un producto no se repite dentro de la misma devolucion: seria contar
        // dos veces los mismos envases.
        builder.HasIndex(r => new { r.DeliveryId, r.ProductId }).IsUnique();

        builder.HasOne(r => r.Delivery)
               .WithMany(d => d.ContainerReturns)
               .HasForeignKey(r => r.DeliveryId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Product)
               .WithMany()
               .HasForeignKey(r => r.ProductId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
