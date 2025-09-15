using CloudCrate.Domain.Entities;
using CloudCrate.Infrastructure.Persistence.Entities;

namespace CloudCrate.Infrastructure.Persistence.Mappers;

public static class CrateFolderEntityMapper
{
    public static CrateFolderEntity ToEntity(this CrateFolder folder, Guid crateId)
    {
        return new CrateFolderEntity
        {
            Id = folder.Id,
            Name = folder.Name,
            Color = folder.Color,
            CrateId = crateId,
            ParentFolderId = folder.ParentFolderId,
            IsRoot = folder.IsRoot,
            CreatedAt = folder.CreatedAt,
            UpdatedAt = folder.UpdatedAt,
            CreatedByUserId = folder.CreatedByUserId,
            IsDeleted = folder.IsDeleted,
            DeletedAt = folder.DeletedAt,
            DeletedByUserId = folder.DeletedByUserId,
            RestoredAt = folder.RestoredAt,
            RestoredByUserId = folder.RestoredByUserId,

            Files = folder.Files.Select(f => f.ToEntity(crateId)).ToList(),
            Subfolders = folder.Subfolders.Select(f => f.ToEntity(crateId)).ToList()
        };
    }

    public static CrateFolder ToDomain(this CrateFolderEntity entity)
    {
        var folder = CrateFolder.Rehydrate(
            id: entity.Id,
            name: entity.Name,
            color: entity.Color,
            parentFolderId: entity.ParentFolderId,
            isRoot: entity.IsRoot,
            crateId: entity.CrateId,
            files: entity.Files.Select(f => f.ToDomain()).ToList(),
            subfolders: entity.Subfolders.Select(f => f.ToDomain()).ToList(),
            createdAt: entity.CreatedAt,
            updatedAt: entity.UpdatedAt,
            createdByUserId: entity.CreatedByUserId,
            isDeleted: entity.IsDeleted,
            deletedAt: entity.DeletedAt,
            deletedByUserId: entity.DeletedByUserId,
            restoredByUserId: entity.RestoredByUserId,
            restoredAt: entity.RestoredAt
        );

        return folder;
    }
}