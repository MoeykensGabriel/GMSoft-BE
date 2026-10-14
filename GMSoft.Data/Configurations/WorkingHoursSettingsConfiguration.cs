using System.Text.Json;
using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class WorkingHoursSettingsConfiguration : IEntityTypeConfiguration<WorkingHoursSettings>
{
    public void Configure(EntityTypeBuilder<WorkingHoursSettings> b)
    {
        b.ToTable("WorkingHoursSettings", t => t.HasCheckConstraint("CK_WorkingHoursSettings_Singleton", "\"Id\" = 1"));
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).ValueGeneratedNever();
        b.Property(s => s.Days).HasColumnType("jsonb")
            .HasConversion(v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<WorkingDay[]>(v, (JsonSerializerOptions?)null)!)
            .Metadata.SetValueComparer(new ValueComparer<WorkingDay[]>(
                (a, b) => a!.SequenceEqual(b!),
                a => a.Aggregate(0, (h, d) => HashCode.Combine(h, d.GetHashCode())), a => a.ToArray()));
    }
}
