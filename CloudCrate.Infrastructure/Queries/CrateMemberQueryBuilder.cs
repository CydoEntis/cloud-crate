using CloudCrate.Application.DTOs.CrateMember.Request;
using CloudCrate.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CloudCrate.Infrastructure.Queries;

public static class CrateMemberQueryBuilder
{
    public static IQueryable<CrateMemberEntity> ApplyMemberSearch(this IQueryable<CrateMemberEntity> query,
        string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return query;

        var term = searchTerm.Trim();
        return query.Where(m =>
            EF.Functions.ILike(m.User.DisplayName, $"%{term}%") ||
            EF.Functions.ILike(m.User.Email, $"%{term}%"));
    }

    public static IQueryable<CrateMemberEntity> ApplyMemberFiltering(this IQueryable<CrateMemberEntity> query,
        CrateMemberQueryParameters parameters)
    {
        // Apply role filter
        if (parameters.FilterByRole.HasValue)
            query = query.Where(m => m.Role == parameters.FilterByRole.Value);

        // Apply recent filter (last 30 days)
        if (parameters.RecentOnly)
            query = query.Where(m => m.JoinedAt >= DateTime.UtcNow.AddDays(-30));

        return query;
    }

    public static IQueryable<CrateMemberEntity> ApplyMemberOrdering(this IQueryable<CrateMemberEntity> query,
        CrateMemberQueryParameters parameters)
    {
        return (parameters.SortBy, parameters.Ascending) switch
        {
            (CrateMemberSortBy.DisplayName, true) => query.OrderBy(m => m.User.DisplayName),
            (CrateMemberSortBy.DisplayName, false) => query.OrderByDescending(m => m.User.DisplayName),
            (CrateMemberSortBy.Email, true) => query.OrderBy(m => m.User.Email),
            (CrateMemberSortBy.Email, false) => query.OrderByDescending(m => m.User.Email),
            (CrateMemberSortBy.Role, true) => query.OrderBy(m => m.Role),
            (CrateMemberSortBy.Role, false) => query.OrderByDescending(m => m.Role),
            (CrateMemberSortBy.JoinedAt, true) => query.OrderBy(m => m.JoinedAt),
            (CrateMemberSortBy.JoinedAt, false) => query.OrderByDescending(m => m.JoinedAt),
            _ => query.OrderByDescending(m => m.JoinedAt) // Default: newest first
        };
    }
}