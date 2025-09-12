using CloudCrate.Application.DTOs.Crate.Request;
using CloudCrate.Domain.Enums;
using CloudCrate.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CloudCrate.Infrastructure.Queries;

public static class CrateQueryBuilder
{
    public static IQueryable<CrateEntity> ApplySearch(this IQueryable<CrateEntity> query, string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return query;

        var term = searchTerm.Trim();

        return query.Where(c => EF.Functions.ILike(c.Name, $"%{term}%"));
    }


    public static IQueryable<CrateEntity> ApplyOrdering(this IQueryable<CrateEntity> query,
        CrateQueryParameters parameters)
    {
        return (parameters.SortBy, parameters.Ascending) switch
        {
            (CrateSortBy.Name, true) => query.OrderBy(c => c.Name),
            (CrateSortBy.Name, false) => query.OrderByDescending(c => c.Name),

            (CrateSortBy.UsedStorage, true) => query.OrderBy(c => c.UsedStorageBytes),
            (CrateSortBy.UsedStorage, false) => query.OrderByDescending(c => c.UsedStorageBytes),

            (CrateSortBy.JoinedAt, true) => query.OrderBy(c => c.Members
                .Where(m => m.Role == CrateRole.Owner)
                .Select(m => m.JoinedAt)
                .FirstOrDefault()),
            (CrateSortBy.JoinedAt, false) => query.OrderByDescending(c => c.Members
                .Where(m => m.Role == CrateRole.Owner)
                .Select(m => m.JoinedAt)
                .FirstOrDefault()),

            // Safety net for any unexpected enum value
            _ => query.OrderBy(c => c.Name)
        };
    }
}