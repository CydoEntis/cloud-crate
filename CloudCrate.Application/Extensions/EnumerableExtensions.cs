using CloudCrate.Application.DTOs.Pagination;

namespace CloudCrate.Application.Extensions;

public static class EnumerableExtensions
{
    public static IEnumerable<List<T>> Batch<T>(this IEnumerable<T> source, int batchSize)
    {
        var batch = new List<T>(batchSize);
        foreach (var item in source)
        {
            batch.Add(item);
            if (batch.Count >= batchSize)
            {
                yield return batch;
                batch = new List<T>(batchSize);
            }
        }

        if (batch.Any())
            yield return batch;
    }

    public static PaginatedResult<T> Paginate<T>(
        this IEnumerable<T> source,
        int page,
        int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        var totalCount = source.Count();
        var items = source
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return PaginatedResult<T>.Create(items, totalCount, page, pageSize);
    }
}