using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class CustomerProductPriceConfiguration : IEntityTypeConfiguration<CustomerProductPrice>
{
    public void Configure(EntityTypeBuilder<CustomerProductPrice> builder)
    {
        builder.ToTable("CustomerProductPrices");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Price).HasPrecision(18, 2);

        // Un solo precio particular por cliente y producto. Con dos filas, cual
        // gana queda a suerte del orden de la consulta.
        builder.HasIndex(p => new { p.CustomerId, p.ProductId }).IsUnique();

        builder.HasOne(p => p.Customer)
               .WithMany(c => c.Prices)
               .HasForeignKey(p => p.CustomerId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Product)
               .WithMany()
               .HasForeignKey(p => p.ProductId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
