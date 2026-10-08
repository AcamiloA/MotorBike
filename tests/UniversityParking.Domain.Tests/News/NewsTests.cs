using UniversityParking.Domain.Common;
using UniversityParking.Domain.News;

namespace UniversityParking.Domain.Tests.News;

public sealed class NewsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
    private static NewsItem Create() => new(" Aviso ", " Contenido ", Guid.NewGuid(), Now);

    [Fact]
    public void News_ShouldStartDraft_WithNoPublicationOrArchiveTime()
    {
        var news = Create();
        Assert.Equal(NewsStatus.DRAFT, news.Status);
        Assert.Null(news.PublishedAt);
        Assert.Null(news.ArchivedAt);
        Assert.Equal("Aviso", news.Title);
        Assert.Equal("Contenido", news.Content);
    }

    [Fact]
    public void PublishAndArchive_ShouldPreserveFirstTransitionTimes()
    {
        var news = Create();
        news.Publish(Now.AddMinutes(1));
        news.Publish(Now.AddMinutes(2));
        Assert.Equal(NewsStatus.PUBLISHED, news.Status);
        Assert.Equal(Now.AddMinutes(1), news.PublishedAt);
        news.Archive(Now.AddMinutes(3));
        news.Archive(Now.AddMinutes(4));
        Assert.Equal(NewsStatus.ARCHIVED, news.Status);
        Assert.Equal(Now.AddMinutes(3), news.ArchivedAt);
        Assert.Equal(Now.AddMinutes(1), news.PublishedAt);
    }

    [Fact]
    public void Draft_ShouldAllowDirectArchiving()
    {
        var news = Create();
        news.Archive(Now);
        Assert.Equal(NewsStatus.ARCHIVED, news.Status);
        Assert.Null(news.PublishedAt);
        Assert.Equal(Now, news.ArchivedAt);
    }

    [Fact]
    public void ArchivedNews_ShouldRejectPublicationAndEditing()
    {
        var news = Create();
        news.Archive(Now);
        Assert.Equal("INVALID_NEWS_STATE", Assert.Throws<DomainException>(() => news.Publish(Now)).Code);
        Assert.Throws<DomainException>(() => news.Update("Otro", "Nuevo", Now));
        Assert.Equal("Aviso", news.Title);
        Assert.Equal(NewsStatus.ARCHIVED, news.Status);
    }

    [Fact]
    public void PublishedNews_ShouldAllowEditing_WithoutChangingPublicationTime()
    {
        var news = Create();
        news.Publish(Now.AddMinutes(1));
        news.Update(" Nuevo título ", " Otro contenido ", Now.AddMinutes(2));
        Assert.Equal("Nuevo título", news.Title);
        Assert.Equal("Otro contenido", news.Content);
        Assert.Equal(NewsStatus.PUBLISHED, news.Status);
        Assert.Equal(Now.AddMinutes(1), news.PublishedAt);
    }

    [Fact]
    public void InvalidNewsUpdate_ShouldNotPartiallyChangeContent()
    {
        var news = Create();
        Assert.Throws<DomainException>(() => news.Update("Nuevo", " ", Now));
        Assert.Equal("Aviso", news.Title);
        Assert.Equal("Contenido", news.Content);
    }

    [Fact]
    public void News_ShouldRejectPublicationBeforeCreation()
    {
        var news = Create();
        Assert.Throws<DomainException>(() => news.Publish(Now.AddSeconds(-1)));
        Assert.Equal(NewsStatus.DRAFT, news.Status);
    }
}
