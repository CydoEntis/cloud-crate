using CloudCrate.Application.DTOs.Admin.Request;
using Microsoft.EntityFrameworkCore;

namespace CloudCrate.Infrastructure.Queries;

public static class AdminQueryBuilder
{
    public static IQueryable<ApplicationUser> ApplyBaseFilters(this IQueryable<ApplicationUser> query,
        bool includeDeleted = true)
    {
        if (!includeDeleted)
        {
            query = query.Where(u => u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow);
        }

        return query;
    }

    public static IQueryable<ApplicationUser> ApplySearch(this IQueryable<ApplicationUser> query, string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return query;

        var term = searchTerm.Trim();
        return query.Where(u =>
            EF.Functions.ILike(u.DisplayName, $"%{term}%") ||
            EF.Functions.ILike(u.Email!, $"%{term}%"));
    }

    public static IQueryable<ApplicationUser> ApplyFilters(this IQueryable<ApplicationUser> query,
        bool? isAdmin = null,
        bool? isLocked = null,
        DateTime? createdAfter = null,
        DateTime? createdBefore = null,
        string? plan = null)
    {
        if (isAdmin.HasValue)
            query = query.Where(u => u.IsAdmin == isAdmin.Value);

        if (isLocked.HasValue)
        {
            if (isLocked.Value)
                query = query.Where(u => u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow);
            else
                query = query.Where(u => u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow);
        }

        if (createdAfter.HasValue)
            query = query.Where(u => u.CreatedAt >= createdAfter.Value);

        if (createdBefore.HasValue)
            query = query.Where(u => u.CreatedAt <= createdBefore.Value);

        if (!string.IsNullOrWhiteSpace(plan))
            query = query.Where(u => u.Plan.ToString() == plan);

        return query;
    }

    public static IQueryable<ApplicationUser> ApplyOrdering(this IQueryable<ApplicationUser> query,
        AdminOrderBy orderBy, bool ascending = true)
    {
        return orderBy switch
        {
            AdminOrderBy.DisplayName => ascending
                ? query.OrderBy(u => u.DisplayName)
                : query.OrderByDescending(u => u.DisplayName),
            AdminOrderBy.Email => ascending
                ? query.OrderBy(u => u.Email)
                : query.OrderByDescending(u => u.Email),
            AdminOrderBy.CreatedAt => ascending
                ? query.OrderBy(u => u.CreatedAt)
                : query.OrderByDescending(u => u.CreatedAt),
            AdminOrderBy.StorageUsed => ascending
                ? query.OrderBy(u => u.UsedStorageBytes)
                : query.OrderByDescending(u => u.UsedStorageBytes),
            _ => query.OrderByDescending(u => u.CreatedAt)
        };
    }

    public static IQueryable<ApplicationUser> ApplyUserFiltering(this IQueryable<ApplicationUser> query,
        AdminUserParameters parameters)
    {
        return query
            .ApplyBaseFilters(parameters.IncludeDeleted)
            .ApplySearch(parameters.SearchTerm)
            .ApplyFilters(parameters.IsAdmin, parameters.IsLocked,
                parameters.CreatedAfter, parameters.CreatedBefore, parameters.Plan)
            .ApplyOrdering(parameters.OrderBy, parameters.Ascending);
    }
}