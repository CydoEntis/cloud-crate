using CloudCrate.Application.DTOs.Folder.Request;
using CloudCrate.Domain.Enums;
using CloudCrate.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CloudCrate.Infrastructure.Queries;

public static class FileQueryBuilder
{
    public static IQueryable<CrateFileEntity> ApplyBaseFilters(this IQueryable<CrateFileEntity> query,
        Guid crateId, Guid? folderId = null, bool includeDeleted = false)
    {
        query = query.Where(f => f.CrateId == crateId);

        if (!includeDeleted)
            query = query.Where(f => !f.IsDeleted);

        if (folderId.HasValue)
            query = query.Where(f => f.CrateFolderId == folderId);
        else
            query = query.Where(f => f.CrateFolderId == null); // Root folder only

        return query;
    }

    public static IQueryable<CrateFileEntity> ApplySearch(this IQueryable<CrateFileEntity> query, string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return query;

        var term = searchTerm.Trim();
        return query.Where(f => EF.Functions.ILike(f.Name, $"%{term}%"));
    }

    public static IQueryable<CrateFileEntity> ApplyFilters(this IQueryable<CrateFileEntity> query,
        long? minSize = null, long? maxSize = null,
        DateTime? createdAfter = null, DateTime? createdBefore = null,
        string? mimeType = null)
    {
        if (minSize.HasValue)
            query = query.Where(f => f.SizeInBytes >= minSize.Value);

        if (maxSize.HasValue)
            query = query.Where(f => f.SizeInBytes <= maxSize.Value);

        if (createdAfter.HasValue)
            query = query.Where(f => f.CreatedAt >= createdAfter.Value);

        if (createdBefore.HasValue)
            query = query.Where(f => f.CreatedAt <= createdBefore.Value);

        if (!string.IsNullOrWhiteSpace(mimeType))
            query = query.Where(f => f.MimeType.StartsWith(mimeType));

        return query;
    }

    public static IQueryable<CrateFileEntity> ApplyOrdering(this IQueryable<CrateFileEntity> query,
        OrderBy orderBy, bool ascending = true)
    {
        return orderBy switch
        {
            OrderBy.Name => ascending
                ? query.OrderBy(f => f.Name)
                : query.OrderByDescending(f => f.Name),

            OrderBy.Size => ascending
                ? query.OrderBy(f => f.SizeInBytes)
                : query.OrderByDescending(f => f.SizeInBytes),

            OrderBy.CreatedAt => ascending
                ? query.OrderBy(f => f.CreatedAt)
                : query.OrderByDescending(f => f.CreatedAt),


            _ => query.OrderBy(f => f.Name)
        };
    }

    public static IQueryable<CrateFileEntity> ApplyFolderContentsFiltering(this IQueryable<CrateFileEntity> query,
        FolderContentsParameters parameters)
    {
        return query
            .ApplyBaseFilters(parameters.CrateId, parameters.FolderId)
            .ApplySearch(parameters.SearchTerm)
            .ApplyFilters(parameters.MinSize, parameters.MaxSize,
                parameters.CreatedAfter, parameters.CreatedBefore)
            .ApplyOrdering(parameters.OrderBy, parameters.Ascending);
    }
}