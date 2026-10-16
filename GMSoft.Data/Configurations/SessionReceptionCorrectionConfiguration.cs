using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class SessionReceptionCorrectionConfiguration : IEntityTypeConfiguration<SessionReceptionCorrection>
{
    public void Configure(EntityTypeBuilder<SessionReceptionCorrection> builder)
    {
        builder.ToTable("SessionReceptionCorrections");
        builder.HasKey(c => c.Id);
        builder.HasIndex(c => new { c.DeliverySessionId, c.CorrectedAt });

        builder.Property(c => c.Reason).HasMaxLength(500).IsRequired();
        builder.Property(c => c.PreviousAmountReceived).HasPrecision(18, 2);
        builder.Property(c => c.NewAmountReceived).HasPrecision(18, 2);

        // Es historial: no se borra con nada.
        builder.HasOne(c => c.DeliverySession)
               .WithMany(s => s.ReceptionCorrections)
               .HasForeignKey(c => c.DeliverySessionId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Lines)
               .WithOne(l => l.Correction)
               .HasForeignKey(l => l.SessionReceptionCorrectionId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SessionReceptionCorrectionLineConfiguration : IEntityTypeConfiguration<SessionReceptionCorrectionLine>
{
    public void Configure(EntityTypeBuilder<SessionReceptionCorrectionLine> builder)
    {
        builder.ToTable("SessionReceptionCorrectionLines");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.State).HasConversion<int>();

        builder.HasOne(l => l.Product)
               .WithMany()
               .HasForeignKey(l => l.ProductId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
