using CloudCrate.Application.DTOs.Admin.Request;
using CloudCrate.Domain.Enums;
using CloudCrate.Infrastructure.Identity;
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
        AdminUserType userType)
    {
        return userType switch
        {
            AdminUserType.Admin => query.Where(u => u.IsAdmin == true),
            AdminUserType.User => query.Where(u => u.IsAdmin == false),
            AdminUserType.All => query,
            _ => query
        };
    }

    public static IQueryable<ApplicationUser> ApplyStatusFilter(this IQueryable<ApplicationUser> query,
        AdminUserStatus userStatus)
    {
        return userStatus switch
        {
            AdminUserStatus.Banned => query.Where(u => u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow),
            AdminUserStatus.Active => query.Where(u => u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow),
            AdminUserStatus.All => query,
            _ => query
        };
    }

    public static IQueryable<ApplicationUser> ApplyPlanFilter(this IQueryable<ApplicationUser> query,
        AdminPlanFilter planFilter)
    {
        return planFilter switch
        {
            AdminPlanFilter.Free => query.Where(u => u.Plan == SubscriptionPlan.Free),
            AdminPlanFilter.Mini => query.Where(u => u.Plan == SubscriptionPlan.Mini),
            AdminPlanFilter.Standard => query.Where(u => u.Plan == SubscriptionPlan.Standard),
            AdminPlanFilter.Max => query.Where(u => u.Plan == SubscriptionPlan.Max),
            AdminPlanFilter.All => query,
            _ => query
        };
    }

    public static IQueryable<ApplicationUser> ApplyOrdering(this IQueryable<ApplicationUser> query,
        AdminUserParameters parameters)
    {
        return (parameters.SortBy, parameters.Ascending) switch
        {
            (AdminUserSortBy.Email, true) => query.OrderBy(u => u.Email),
            (AdminUserSortBy.Email, false) => query.OrderByDescending(u => u.Email),
            (AdminUserSortBy.DisplayName, true) => query.OrderBy(u => u.DisplayName),
            (AdminUserSortBy.DisplayName, false) => query.OrderByDescending(u => u.DisplayName),
            (AdminUserSortBy.CreatedAt, true) => query.OrderBy(u => u.CreatedAt),
            (AdminUserSortBy.CreatedAt, false) => query.OrderByDescending(u => u.CreatedAt),
            (AdminUserSortBy.StorageUsed, true) => query.OrderBy(u => u.UsedStorageBytes),
            (AdminUserSortBy.StorageUsed, false) => query.OrderByDescending(u => u.UsedStorageBytes),
            (AdminUserSortBy.Plan, true) => query.OrderBy(u => u.Plan),
            (AdminUserSortBy.Plan, false) => query.OrderByDescending(u => u.Plan),
            // Safety net for any unexpected enum value
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
            .ApplyOrdering(parameters);
    }
}