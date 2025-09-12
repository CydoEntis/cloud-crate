using CloudCrate.Domain.Entities;
using CloudCrate.Domain.ValueObjects;
using CloudCrate.Infrastructure.Persistence.Entities;

namespace CloudCrate.Infrastructure.Persistence.Mappers
{
    public static class CrateFileMapper
    {
        public static CrateFileEntity ToEntity(this CrateFile file, Guid crateId)
        {
            return new CrateFileEntity
            {
                Id = file.Id,
                Name = file.Name,
                SizeInBytes = file.Size.Bytes,
                MimeType = file.MimeType,
                ObjectKey = file.ObjectKey,
                CrateId = crateId,
                CrateFolderId = file.CrateFolderId,
                UploadedByUserId = file.UploadedByUserId,
                CreatedAt = file.CreatedAt,
                UpdatedAt = file.UpdatedAt,
                IsDeleted = file.IsDeleted,
                DeletedAt = file.DeletedAt,
                DeletedByUserId = file.DeletedByUserId,
                RestoredAt = file.RestoredAt,
                RestoredByUserId = file.RestoredByUserId
            };
        }

        public static CrateFile ToDomain(this CrateFileEntity entity)
        {
            return CrateFile.Rehydrate(
                entity.Id,
                entity.Name,
                StorageSize.FromBytes(entity.SizeInBytes),
                entity.MimeType,
                entity.ObjectKey,
                entity.CrateId,
                entity.CrateFolderId,
                entity.UploadedByUserId,
                entity.IsDeleted,
                entity.DeletedAt,
                entity.DeletedByUserId,
                entity.RestoredByUserId,
                entity.RestoredAt,
                entity.CreatedAt,
                entity.UpdatedAt
            );
        }
    }
}