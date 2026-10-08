using UniversityParking.Application.Common.Pagination;

namespace UniversityParking.Application.Tests.Common;

public sealed class PaginationTests
{
    [Fact]
    public void PageRequest_ShouldUseCanonicalDefaults()
    {
        var request = new PageRequest();
        Assert.Equal(1, request.Page);
        Assert.Equal(20, request.PageSize);
        Assert.Equal(0, request.Offset);
        Assert.True(new PageRequestValidator().Validate(request).IsValid);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    [InlineData(1, 101)]
    public void PageRequest_ShouldRejectInvalidPageOrSize(int page, int size) =>
        Assert.False(new PageRequestValidator().Validate(new PageRequest(page, size)).IsValid);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 100)]
    [InlineData(int.MaxValue, 100)]
    public void PageRequest_ShouldAcceptValidBoundariesWithoutArtificialPageLimit(int page, int size)
    {
        var request = new PageRequest(page, size);
        Assert.True(new PageRequestValidator().Validate(request).IsValid);
        Assert.Equal(((long)page - 1) * size, request.Offset);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(20, 1)]
    [InlineData(21, 2)]
    [InlineData(long.MaxValue, 461168601842738791L)]
    public void PagedResult_ShouldCalculateTotalPagesWithoutOverflow(long count, long pages)
    {
        var result = new PagedResult<int>([], 1, 20, count);
        Assert.Equal(pages, result.TotalPages);
        Assert.Equal(count, result.TotalCount);
    }

    [Fact]
    public void EmptyCollection_ShouldRemainSuccessfulPageWithZeroTotals()
    {
        var result = new PagedResult<string>([], 1, 20, 0);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public void PagedResult_ShouldSnapshotItems()
    {
        var items = new List<string> { "Uno" };
        var result = new PagedResult<string>(items, 1, 20, 1);
        items.Clear();
        Assert.Equal("Uno", Assert.Single(result.Items));
    }
}
