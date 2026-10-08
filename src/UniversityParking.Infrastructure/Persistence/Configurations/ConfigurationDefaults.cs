using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversityParking.Domain.Common;

namespace UniversityParking.Infrastructure.Persistence.Configurations;

internal static class ConfigurationDefaults
{
    internal static void Entity<TEntity>(EntityTypeBuilder<TEntity> builder, string table) where TEntity : Entity
    {
        builder.ToTable(table);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
    }
}
