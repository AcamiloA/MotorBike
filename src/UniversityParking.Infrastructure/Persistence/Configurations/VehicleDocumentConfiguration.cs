using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Infrastructure.Persistence.Configurations;

public sealed class VehicleDocumentConfiguration : IEntityTypeConfiguration<VehicleDocument>
{
    public void Configure(EntityTypeBuilder<VehicleDocument> builder)
    {
        ConfigurationDefaults.Entity(builder, "vehicle_documents");
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.DocumentNumber).HasMaxLength(100);
        builder.Property(x => x.StorageKey).HasMaxLength(500).IsRequired();
        builder.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SizeBytes).HasColumnType("bigint");
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_vehicle_documents_type", "type IN ('VEHICLE_REGISTRATION', 'INSURANCE', 'OWNERSHIP_SUPPORT', 'OTHER')");
            table.HasCheckConstraint("ck_vehicle_documents_size", "size_bytes > 0");
            table.HasCheckConstraint("ck_vehicle_documents_dates", "issued_on IS NULL OR expires_on IS NULL OR expires_on >= issued_on");
        });
    }
}
