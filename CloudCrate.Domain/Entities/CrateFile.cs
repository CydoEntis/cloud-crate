using CloudCrate.Domain.ValueObjects;

namespace CloudCrate.Domain.Entities
{
    public class CrateFile
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public StorageSize Size { get; private set; }
        public string MimeType { get; private set; } = string.Empty;
        public string ObjectKey { get; private set; } = string.Empty;
        public Guid CrateId { get; private set; }
        public Crate Crate { get; set; }

        public Guid? CrateFolderId { get; private set; }
        public CrateFolder? Folder { get; set; }

        public string UploadedByUserId { get; private set; } = string.Empty;
        public CrateMember UploadedByUser { get; set; } = null!;
        public bool IsDeleted { get; private set; } = false;
        public DateTime? DeletedAt { get; private set; }

        public string? DeletedByUserId { get; private set; }
        public UserAccount? DeletedByUser { get; private set; }

        public string? RestoredByUserId { get; private set; }
        public UserAccount? RestoredByUser { get; private set; }
        public DateTime? RestoredAt { get; private set; }

        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        private CrateFile()
        {
        }

        internal CrateFile(
            Guid id,
            string name,
            StorageSize size,
            string mimeType,
            string objectKey,
            Guid crateId,
            Guid? crateFolderId,
            string uploadedByUserId,
            bool isDeleted,
            DateTime? deletedAt,
            string? deletedByUserId,
            string? restoredByUserId,
            DateTime? restoredAt,
            DateTime createdAt,
            DateTime updatedAt)
        {
            Id = id;
            Name = name;
            Size = size;
            MimeType = mimeType;
            ObjectKey = objectKey;
            CrateId = crateId;
            CrateFolderId = crateFolderId;
            UploadedByUserId = uploadedByUserId;
            IsDeleted = isDeleted;
            DeletedAt = deletedAt;
            DeletedByUserId = deletedByUserId;
            RestoredByUserId = restoredByUserId;
            RestoredAt = restoredAt;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
        }

        public static CrateFile Rehydrate(
            Guid id,
            string name,
            StorageSize size,
            string mimeType,
            string objectKey,
            Guid crateId,
            Guid? folderId,
            string uploadedByUserId,
            bool isDeleted,
            DateTime? deletedAt,
            string? deletedByUserId,
            string? restoredByUserId,
            DateTime? restoredAt,
            DateTime createdAt,
            DateTime updatedAt)
        {
            return new CrateFile(
                id, name, size, mimeType, objectKey, crateId, folderId,
                uploadedByUserId, isDeleted, deletedAt, deletedByUserId,
                restoredByUserId, restoredAt, createdAt, updatedAt
            );
        }

        public static CrateFile Create(
            string name,
            StorageSize size,
            string mimeType,
            Guid crateId,
            string uploadedByUserId,
            Guid? folderId = null)
        {
            return new CrateFile
            {
                Id = Guid.NewGuid(),
                Name = name,
                Size = size,
                MimeType = mimeType,
                CrateId = crateId,
                CrateFolderId = folderId,
                UploadedByUserId = uploadedByUserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false
            };
        }
        
        public void SoftDelete(string? deletedByUserId = null)
        {
            IsDeleted = true;
            DeletedAt = DateTime.UtcNow;
            DeletedByUserId = deletedByUserId;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Restore(string? restoredByUserId = null)
        {
            IsDeleted = false;
            RestoredAt = DateTime.UtcNow;
            RestoredByUserId = restoredByUserId;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetObjectKey(string objectKey)
        {
            if (string.IsNullOrWhiteSpace(objectKey))
                throw new ArgumentException("ObjectKey cannot be null or empty", nameof(objectKey));
                
            ObjectKey = objectKey;
            UpdatedAt = DateTime.UtcNow;
        }

        public void MoveTo(Guid? newFolderId)
        {
            CrateFolderId = newFolderId;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}