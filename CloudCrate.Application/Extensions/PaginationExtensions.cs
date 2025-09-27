using CloudCrate.Application.DTOs.Pagination;

namespace CloudCrate.Application.Extensions;

public static class PaginationExtensions
{
    public static PaginatedResult<T> ToPaginatedResult<T>(this List<T> items, int page, int pageSize)
    {
        var total = items.Count;
        var pagedItems = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return PaginatedResult<T>.Create(pagedItems, total, page, pageSize);
    }
}
