using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversityParking.Domain.News;
using UniversityParking.Domain.Users;

namespace UniversityParking.Infrastructure.Persistence.Configurations;

public sealed class NewsItemConfiguration : IEntityTypeConfiguration<NewsItem>
{
    public void Configure(EntityTypeBuilder<NewsItem> builder)
    {
        ConfigurationDefaults.Entity(builder, "news");
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Content).HasColumnType("text").IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.Status).HasDatabaseName("ix_news_status");
        builder.HasIndex(x => x.PublishedAt).IsDescending().HasDatabaseName("ix_news_published_at");
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_news_status", "status IN ('DRAFT', 'PUBLISHED', 'ARCHIVED')");
            table.HasCheckConstraint("ck_news_state", "(status = 'DRAFT' AND published_at IS NULL AND archived_at IS NULL) OR (status = 'PUBLISHED' AND published_at IS NOT NULL AND archived_at IS NULL) OR (status = 'ARCHIVED' AND archived_at IS NOT NULL)");
        });
    }
}
