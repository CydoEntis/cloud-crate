using CloudCrate.Application.DTOs.Crate.Request;
using CloudCrate.Domain.Entities;
using CloudCrate.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace CloudCrate.Application.Queries;

public static class CrateQueryBuilder
{
    public static IQueryable<Crate> ApplySearch(this IQueryable<Crate> query, string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return query;

        var term = searchTerm.Trim();

        return query.Where(c => EF.Functions.ILike(c.Name, $"%{term}%"));
    }


    public static IQueryable<Crate> ApplyOrdering(this IQueryable<Crate> query, CrateQueryParameters parameters)
    {
        return (parameters.SortBy, parameters.Ascending) switch
        {
            (CrateSortBy.Name, true) => query.OrderBy(c => c.Name),
            (CrateSortBy.Name, false) => query.OrderByDescending(c => c.Name),

            (CrateSortBy.UsedStorage, true) => query.OrderBy(c => c.UsedStorage.Bytes),
            (CrateSortBy.UsedStorage, false) => query.OrderByDescending(c => c.UsedStorage.Bytes),

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