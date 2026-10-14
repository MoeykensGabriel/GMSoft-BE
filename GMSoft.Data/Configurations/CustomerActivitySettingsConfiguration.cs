using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class CustomerActivitySettingsConfiguration : IEntityTypeConfiguration<CustomerActivitySettings>
{
    public void Configure(EntityTypeBuilder<CustomerActivitySettings> b)
    {
        b.ToTable("CustomerActivitySettings", t =>
        {
            t.HasCheckConstraint("CK_CustomerActivitySettings_Singleton", "\"Id\" = 1");
            t.HasCheckConstraint("CK_CustomerActivitySettings_Order",
                "\"RedAfterMissedWeeks\" > 0 AND \"BlackAfterMissedWeeks\" > \"RedAfterMissedWeeks\"");
        });
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).ValueGeneratedNever();
    }
}
