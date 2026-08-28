using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class ZoneConfiguration : IEntityTypeConfiguration<Zone>
{
    public void Configure(EntityTypeBuilder<Zone> builder)
    {
        builder.ToTable("Zones");
        builder.HasKey(z => z.Id);

        builder.Property(z => z.Name).IsRequired().HasMaxLength(100);
        builder.Property(z => z.Notes).HasMaxLength(500);

        // Dos zonas con el mismo nombre se eligen mal al abrir la sesion, y el
        // chofer termina repartiendo el recorrido equivocado.
        builder.HasIndex(z => z.Name).IsUnique();
    }
}
