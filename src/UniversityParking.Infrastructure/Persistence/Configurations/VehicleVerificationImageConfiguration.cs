using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Infrastructure.Persistence.Configurations;

public sealed class VehicleVerificationImageConfiguration : IEntityTypeConfiguration<VehicleVerificationImage>
{
    public void Configure(EntityTypeBuilder<VehicleVerificationImage> builder)
    {
        ConfigurationDefaults.Entity(builder, "vehicle_verification_images");
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(500).IsRequired();
        builder.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SizeBytes).HasColumnType("bigint");
        builder.HasIndex(x => x.VehicleId).IsUnique().HasDatabaseName("ux_vehicle_verification_images_vehicle");
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_vehicle_verification_images_type", "type IN ('TRANSIT_LICENSE_FRONT', 'BICYCLE_PHOTO')");
            table.HasCheckConstraint("ck_vehicle_verification_images_size", "size_bytes > 0 AND size_bytes <= 5242880");
            table.HasCheckConstraint("ck_vehicle_verification_images_mime", "content_type IN ('image/jpeg', 'image/png')");
        });
    }
}
