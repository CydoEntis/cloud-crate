using CloudCrate.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CloudCrate.Infrastructure.Queries;

public static class FolderQueryBuilder
{
    public static IQueryable<CrateFolderEntity> ApplyBaseFilters(
        this IQueryable<CrateFolderEntity> query,
        Guid crateId,
        bool includeDeleted = false)
    {
        query = query.Where(f => f.CrateId == crateId);
        if (!includeDeleted)
            query = query.Where(f => !f.IsDeleted);
        return query;
    }

    public static IQueryable<CrateFolderEntity> ApplySearch(
        this IQueryable<CrateFolderEntity> query,
        string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return query;
        var term = searchTerm.Trim();
        return query.Where(f => EF.Functions.ILike(f.Name, $"%{term}%"));
    }

    public static IQueryable<CrateFolderEntity> ApplyExclusion(
        this IQueryable<CrateFolderEntity> query,
        HashSet<Guid> excludedIds)
    {
        if (excludedIds.Count == 0)
            return query;
        return query.Where(f => !excludedIds.Contains(f.Id));
    }

    public static IQueryable<CrateFolderEntity> ApplyOrdering(
        this IQueryable<CrateFolderEntity> query,
        bool ascending = true)
    {
        return ascending
            ? query.OrderBy(f => f.Name)
            : query.OrderByDescending(f => f.Name);
    }

    public static IQueryable<CrateFolderEntity> ApplyMoveTargetFiltering(
        this IQueryable<CrateFolderEntity> query,
        Guid crateId,
        HashSet<Guid> excludedIds,
        string? searchTerm = null,
        bool ascending = true)
    {
        return query
            .ApplyBaseFilters(crateId)
            .ApplyExclusion(excludedIds)
            .ApplySearch(searchTerm)
            .ApplyOrdering(ascending);
    }
}