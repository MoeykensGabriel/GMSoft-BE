using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class SessionCashSettlementConfiguration : IEntityTypeConfiguration<SessionCashSettlement>
{
    public void Configure(EntityTypeBuilder<SessionCashSettlement> builder)
    {
        builder.ToTable("SessionCashSettlements");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.AmountReceived).HasPrecision(18, 2);
        builder.Property(s => s.Notes).HasMaxLength(500);

        // Una rendicion por sesion.
        builder.HasOne(s => s.DeliverySession)
               .WithOne(d => d.CashSettlement)
               .HasForeignKey<SessionCashSettlement>(s => s.DeliverySessionId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
