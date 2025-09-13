using CloudCrate.Application.DTOs.CrateMember.Request;
using CloudCrate.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CloudCrate.Infrastructure.Queries;

public static class CrateMemberQueryBuilder
{
    public static IQueryable<CrateMemberEntity> ApplyMemberSearch(this IQueryable<CrateMemberEntity> query, string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return query;

        var term = searchTerm.Trim();

        return query.Where(m => 
            EF.Functions.ILike(m.User.DisplayName, $"%{term}%") ||
            EF.Functions.ILike(m.User.Email, $"%{term}%"));
    }

    public static IQueryable<CrateMemberEntity> ApplyMemberOrdering(this IQueryable<CrateMemberEntity> query,
        CrateMemberQueryParameters parameters)
    {
        var baseQuery = parameters.FilterByRole.HasValue 
            ? query.Where(m => m.Role == parameters.FilterByRole.Value)
            : query;

        return (parameters.SortBy, parameters.Ascending) switch
        {
            (CrateMemberSortBy.DisplayName, true) => baseQuery.OrderBy(m => m.User.DisplayName),
            (CrateMemberSortBy.DisplayName, false) => baseQuery.OrderByDescending(m => m.User.DisplayName),

            (CrateMemberSortBy.Email, true) => baseQuery.OrderBy(m => m.User.Email),
            (CrateMemberSortBy.Email, false) => baseQuery.OrderByDescending(m => m.User.Email),

            (CrateMemberSortBy.Role, true) => baseQuery.OrderBy(m => m.Role),
            (CrateMemberSortBy.Role, false) => baseQuery.OrderByDescending(m => m.Role),

            (CrateMemberSortBy.JoinedAt, true) => baseQuery.OrderBy(m => m.JoinedAt),
            (CrateMemberSortBy.JoinedAt, false) => baseQuery.OrderByDescending(m => m.JoinedAt),

            _ => baseQuery.OrderByDescending(m => m.JoinedAt) // Default: newest first
        };
    }
}