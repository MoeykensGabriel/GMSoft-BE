using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Detail).IsRequired().HasMaxLength(200);
        builder.Property(p => p.CommercialDetail).HasMaxLength(200);
        builder.Property(p => p.ImageUrl).HasMaxLength(500);

        // Precision explicita: sin esto Postgres crea un numeric sin limite y el
        // redondeo queda a criterio de cada query.
        builder.Property(p => p.SalePrice).HasPrecision(18, 2);

        builder.Property(p => p.Tracking).HasConversion<int>();

        // El catalogo del reparto siempre filtra por publicado.
        builder.HasIndex(p => p.IsPublished);
    }
}
