using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversityParking.Domain.Vehicles;
namespace UniversityParking.Infrastructure.Persistence.Configurations;
public sealed class PendingFileDeletionConfiguration:IEntityTypeConfiguration<PendingFileDeletion>
{
    public void Configure(EntityTypeBuilder<PendingFileDeletion> builder)
    {
        ConfigurationDefaults.Entity(builder,"pending_file_deletions");builder.Property(x=>x.StorageKey).HasMaxLength(500).IsRequired();
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(x=>x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x=>x.CompletedAt);
    }
}
