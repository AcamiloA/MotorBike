using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversityParking.Domain.Universities;

namespace UniversityParking.Infrastructure.Persistence.Configurations;

public sealed class UniversityConfiguration : IEntityTypeConfiguration<University>
{
    public void Configure(EntityTypeBuilder<University> builder)
    {
        ConfigurationDefaults.Entity(builder, "universities");
        builder.Property(x => x.Code).HasMaxLength(University.CodeMaximumLength).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(University.NameMaximumLength).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ux_universities_code");
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_universities_code", "code <> '' AND code = upper(btrim(code))");
            table.HasCheckConstraint("ck_universities_name", "btrim(name) <> ''");
            table.HasCheckConstraint("ck_universities_dates", "updated_at >= created_at");
        });
        // Reference data is independent of all optional demo seed settings.
        var created = new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);
        builder.HasData(
            new University(UniversityIds.Etitc, "ETITC", "ETITC", created),
            new University(UniversityIds.Cmc, "CMC", "Colegio Mayor de Cundinamarca", created),
            new University(UniversityIds.Upn, "UPN", "U. Pedagógica", created));
    }
}
