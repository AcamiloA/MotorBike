namespace UniversityParking.Application.Common.Pagination;

public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; }
    public int Page { get; }
    public int PageSize { get; }
    public long TotalCount { get; }
    public long TotalPages => TotalCount / PageSize + (TotalCount % PageSize == 0 ? 0 : 1);

    public PagedResult(IEnumerable<T> items, int page, int pageSize, long totalCount)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > PageRequest.MaximumPageSize) throw new ArgumentOutOfRangeException(nameof(pageSize));
        if (totalCount < 0) throw new ArgumentOutOfRangeException(nameof(totalCount));
        var snapshot = items.ToArray();
        if (snapshot.Length > pageSize || snapshot.LongLength > totalCount)
            throw new ArgumentException("Los elementos no corresponden a los límites de la página.", nameof(items));
        Items = Array.AsReadOnly(snapshot);
        Page = page;
        PageSize = pageSize;
        TotalCount = totalCount;
    }
}
