using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class ContainerUnitConfiguration : IEntityTypeConfiguration<ContainerUnit>
{
    public void Configure(EntityTypeBuilder<ContainerUnit> builder)
    {
        builder.ToTable("ContainerUnits");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.SerialNumber).IsRequired().HasMaxLength(50);
        builder.Property(u => u.Status).HasConversion<int>();

        // El numero identifica al envase: si se repite, deja de identificar nada.
        builder.HasIndex(u => u.SerialNumber).IsUnique();

        // Que envases tiene un cliente, y que hay en deposito.
        builder.HasIndex(u => new { u.CurrentCustomerId, u.Status });

        builder.HasOne(u => u.Product)
               .WithMany()
               .HasForeignKey(u => u.ProductId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.CurrentCustomer)
               .WithMany()
               .HasForeignKey(u => u.CurrentCustomerId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
