using CloudCrate.Application.DTOs.Admin.Request;
using CloudCrate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CloudCrate.Infrastructure.Queries;

public static class AdminQueryBuilder
{
    public static IQueryable<ApplicationUser> ApplySearch(this IQueryable<ApplicationUser> query, string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return query;

        var term = searchTerm.Trim();
        return query.Where(u =>
            EF.Functions.ILike(u.DisplayName, $"%{term}%") ||
            EF.Functions.ILike(u.Email!, $"%{term}%"));
    }

    public static IQueryable<ApplicationUser> ApplyUserTypeFilter(this IQueryable<ApplicationUser> query,
        string? userType)
    {
        return userType switch
        {
            "Admin" => query.Where(u => u.IsAdmin == true),
            "User" => query.Where(u => u.IsAdmin == false),
            _ => query
        };
    }

    public static IQueryable<ApplicationUser> ApplyStatusFilter(this IQueryable<ApplicationUser> query,
        string? userStatus)
    {
        return userStatus switch
        {
            "Banned" => query.Where(u => u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow),
            "Active" => query.Where(u => u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow),
            _ => query
        };
    }

    public static IQueryable<ApplicationUser> ApplyPlanFilter(this IQueryable<ApplicationUser> query,
        string? planFilter)
    {
        if (string.IsNullOrWhiteSpace(planFilter) || planFilter == "All")
            return query;

        return query.Where(u => u.Plan.ToString() == planFilter);
    }

    public static IQueryable<ApplicationUser> ApplySorting(this IQueryable<ApplicationUser> query, string? sortBy,
        bool ascending)
    {
        return sortBy?.ToLower() switch
        {
            "email" => ascending ? query.OrderBy(u => u.Email) : query.OrderByDescending(u => u.Email),
            "displayname" => ascending
                ? query.OrderBy(u => u.DisplayName)
                : query.OrderByDescending(u => u.DisplayName),
            "createdat" => ascending ? query.OrderBy(u => u.CreatedAt) : query.OrderByDescending(u => u.CreatedAt),
            "storageused" => ascending
                ? query.OrderBy(u => u.UsedStorageBytes)
                : query.OrderByDescending(u => u.UsedStorageBytes),
            _ => query.OrderByDescending(u => u.CreatedAt)
        };
    }

    public static IQueryable<ApplicationUser> ApplyUserFiltering(this IQueryable<ApplicationUser> query,
        AdminUserParameters parameters)
    {
        return query
            .ApplySearch(parameters.SearchTerm)
            .ApplyUserTypeFilter(parameters.UserType)
            .ApplyStatusFilter(parameters.UserStatus)
            .ApplyPlanFilter(parameters.PlanFilter)
            .ApplySorting(parameters.SortBy, parameters.Ascending);
    }
}