using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class CustomerContainerBalanceConfiguration : IEntityTypeConfiguration<CustomerContainerBalance>
{
    public void Configure(EntityTypeBuilder<CustomerContainerBalance> builder)
    {
        builder.ToTable("CustomerContainerBalances");
        builder.HasKey(b => b.Id);

        // Una sola fila por cliente y producto. Sin este unico, dos entregas
        // simultaneas al mismo cliente crean dos saldos y el total queda partido.
        builder.HasIndex(b => new { b.CustomerId, b.ProductId }).IsUnique();

        builder.HasOne(b => b.Customer)
               .WithMany()
               .HasForeignKey(b => b.CustomerId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Product)
               .WithMany()
               .HasForeignKey(b => b.ProductId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
