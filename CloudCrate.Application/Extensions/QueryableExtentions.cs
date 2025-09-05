using CloudCrate.Application.DTOs.Pagination;
using Microsoft.EntityFrameworkCore;

namespace CloudCrate.Application.Extensions;

public static class QueryableExtensions
{
    public static async Task<PaginatedResult<T>> PaginateAsync<T>(
        this IQueryable<T> query,
        int page,
        int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return PaginatedResult<T>.Create(items, totalCount, page, pageSize);
    }
}