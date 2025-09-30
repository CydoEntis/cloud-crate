using CloudCrate.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CloudCrate.Infrastructure.Queries;

public static class TrashQueryBuilder
{
    public static IQueryable<CrateFileEntity> ApplyTrashFileFiltering(
        this IQueryable<CrateFileEntity> query,
        Guid crateId,
        string userId,
        bool isOwnerOrManager,
        HashSet<Guid> deletedFolderIds)
    {
        query = query.Where(f => f.CrateId == crateId && f.IsDeleted);

        if (!isOwnerOrManager)
        {
            query = query.Where(f =>
                f.UploadedByUserId == userId ||
                f.DeletedByUserId == userId);
        }

        query = query.Where(f =>
            f.CrateFolderId == null ||
            !deletedFolderIds.Contains(f.CrateFolderId.Value));

        return query;
    }

    public static IQueryable<CrateFolderEntity> ApplyTrashFolderFiltering(
        this IQueryable<CrateFolderEntity> query,
        Guid crateId,
        string userId,
        bool isOwnerOrManager,
        HashSet<Guid> deletedFolderIds)
    {
        query = query.Where(f => f.CrateId == crateId && f.IsDeleted);

        if (!isOwnerOrManager)
        {
            query = query.Where(f =>
                f.CreatedByUserId == userId ||
                f.DeletedByUserId == userId);
        }

        query = query.Where(f =>
            f.ParentFolderId == null ||
            !deletedFolderIds.Contains(f.ParentFolderId.Value));

        return query;
    }

    public static IQueryable<T> ApplyTrashSearch<T>(
        this IQueryable<T> query,
        string? searchTerm) where T : class
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return query;

        var normalizedSearchTerm = searchTerm.Trim().ToLowerInvariant();

        return query.Where(item =>
            EF.Functions.ILike(EF.Property<string>(item, "Name"), $"%{normalizedSearchTerm}%"));
    }
}