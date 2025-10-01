using CloudCrate.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CloudCrate.Infrastructure.Queries;

public static class TrashQueryBuilder
{
    public static IQueryable<CrateFileEntity> ApplyTrashFileFiltering(
        this IQueryable<CrateFileEntity> query,
        List<Guid> crateIds,
        string userId,
        List<Guid> ownerManagerCrateIds,
        List<Guid> memberCrateIds,
        HashSet<Guid> deletedFolderIds)
    {
        query = query.Where(f => crateIds.Contains(f.CrateId) && f.IsDeleted);

        // Owners/Managers see everything in their crates, Members only see their own
        query = query.Where(f =>
            ownerManagerCrateIds.Contains(f.CrateId) ||
            (memberCrateIds.Contains(f.CrateId) &&
             (f.UploadedByUserId == userId || f.DeletedByUserId == userId)));

        return query;
    }

    public static IQueryable<CrateFolderEntity> ApplyTrashFolderFiltering(
        this IQueryable<CrateFolderEntity> query,
        List<Guid> crateIds,
        string userId,
        List<Guid> ownerManagerCrateIds,
        List<Guid> memberCrateIds,
        HashSet<Guid> deletedFolderIds)
    {
        query = query.Where(f => crateIds.Contains(f.CrateId) && f.IsDeleted);

        // Owners/Managers see everything in their crates, Members only see their own
        query = query.Where(f =>
            ownerManagerCrateIds.Contains(f.CrateId) ||
            (memberCrateIds.Contains(f.CrateId) &&
             (f.CreatedByUserId == userId || f.DeletedByUserId == userId)));

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