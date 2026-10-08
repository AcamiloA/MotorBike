using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversityParking.Domain.AcademicPeriods;

namespace UniversityParking.Infrastructure.Persistence.Configurations;

public sealed class AcademicPeriodConfiguration : IEntityTypeConfiguration<AcademicPeriod>
{
    public void Configure(EntityTypeBuilder<AcademicPeriod> builder)
    {
        ConfigurationDefaults.Entity(builder, "academic_periods");
        builder.Property(x => x.Name).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("ux_academic_periods_name");
        builder.HasIndex(x => x.Status).IsUnique().HasFilter("status = 'ACTIVE'").HasDatabaseName("ux_academic_periods_single_active");
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_academic_periods_status", "status IN ('PLANNED', 'ACTIVE', 'CLOSED')");
            table.HasCheckConstraint("ck_academic_periods_dates", "starts_on < ends_on");
        });
    }
}
