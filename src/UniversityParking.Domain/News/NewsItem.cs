using UniversityParking.Domain.Common;

namespace UniversityParking.Domain.News;

public enum NewsStatus { DRAFT, PUBLISHED, ARCHIVED }

public sealed class NewsItem : Entity
{
    public string Title { get; private set; }
    public string Content { get; private set; }
    public NewsStatus Status { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public NewsItem(string title, string content, Guid createdBy, DateTimeOffset createdAt)
    {
        Title = Guard.Text(title, 200, "título");
        Content = Guard.Text(content, int.MaxValue, "contenido");
        CreatedBy = Guard.Id(createdBy, "autor");
        CreatedAt = UpdatedAt = Guard.Utc(createdAt);
        Status = NewsStatus.DRAFT;
    }

    public void Update(string title, string content, DateTimeOffset updatedAt)
    {
        RejectArchived();
        var validTitle = Guard.Text(title, 200, "título");
        var validContent = Guard.Text(content, int.MaxValue, "contenido");
        var now = Guard.Utc(updatedAt);
        Guard.Chronology(now, UpdatedAt);
        Title = validTitle;
        Content = validContent;
        UpdatedAt = now;
    }

    public void Publish(DateTimeOffset publishedAt)
    {
        RejectArchived();
        if (Status == NewsStatus.PUBLISHED) return;
        var now = Guard.Utc(publishedAt);
        Guard.Chronology(now, UpdatedAt);
        PublishedAt = UpdatedAt = now;
        Status = NewsStatus.PUBLISHED;
    }

    public void Archive(DateTimeOffset archivedAt)
    {
        if (Status == NewsStatus.ARCHIVED) return;
        var now = Guard.Utc(archivedAt);
        Guard.Chronology(now, UpdatedAt);
        ArchivedAt = UpdatedAt = now;
        Status = NewsStatus.ARCHIVED;
    }

    private void RejectArchived()
    {
        if (Status == NewsStatus.ARCHIVED)
            throw new DomainException("INVALID_NEWS_STATE", "La noticia está archivada.");
    }
}
