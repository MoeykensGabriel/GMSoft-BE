using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.Method).HasConversion<int>();
        builder.Property(p => p.Notes).HasMaxLength(500);

        // El saldo del cliente suma sus pagos; el estado de cuenta los ordena por fecha.
        builder.HasIndex(p => new { p.CustomerId, p.PaidAt });

        builder.HasOne(p => p.Customer)
               .WithMany()
               .HasForeignKey(p => p.CustomerId)
               .OnDelete(DeleteBehavior.Restrict);

        // Que chofer lo trajo. Opcional: una transferencia a la oficina no tiene sesion.
        builder.HasOne(p => p.DeliverySession)
               .WithMany()
               .HasForeignKey(p => p.DeliverySessionId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
