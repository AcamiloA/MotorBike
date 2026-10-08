using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversityParking.Domain.AcademicPeriods;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Infrastructure.Persistence.Configurations;

public sealed class VehicleRegistrationConfiguration : IEntityTypeConfiguration<VehicleRegistration>
{
    public void Configure(EntityTypeBuilder<VehicleRegistration> builder)
    {
        ConfigurationDefaults.Entity(builder, "vehicle_registrations");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.CancelReason).HasMaxLength(200);
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CancelledBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AcademicPeriod>().WithMany().HasForeignKey(x => x.AcademicPeriodId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.VehicleId, x.UserId, x.AcademicPeriodId }).IsUnique().HasDatabaseName("ux_vehicle_registrations_vehicle_user_period");
        builder.HasIndex(x => new { x.VehicleId, x.AcademicPeriodId }).HasDatabaseName("ix_vehicle_registrations_vehicle_period");
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_vehicle_registrations_status", "status IN ('ACTIVE', 'CANCELLED')");
            table.HasCheckConstraint("ck_vehicle_registrations_cancellation", "(status = 'ACTIVE' AND cancelled_at IS NULL AND cancelled_by IS NULL AND cancel_reason IS NULL) OR (status = 'CANCELLED' AND cancelled_at IS NOT NULL AND cancel_reason IS NOT NULL)");
        });
    }
}
