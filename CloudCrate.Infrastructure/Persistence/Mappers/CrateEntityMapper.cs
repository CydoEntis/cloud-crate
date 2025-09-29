using CloudCrate.Domain.Entities;
using CloudCrate.Domain.ValueObjects;
using CloudCrate.Infrastructure.Persistence.Entities;

namespace CloudCrate.Infrastructure.Persistence.Mappers;

public static class CrateEntityMapper
{
    public static CrateEntity ToEntity(this Crate crate)
    {
        return new CrateEntity
        {
            Id = crate.Id,
            Name = crate.Name,
            Color = crate.Color,
            AllocatedStorageBytes = crate.AllocatedStorage.Bytes,
            UsedStorageBytes = crate.UsedStorage.Bytes,
            CreatedAt = crate.CreatedAt,
            UpdatedAt = crate.UpdatedAt,
            LastAccessedAt = crate.LastAccessedAt,
            Folders = crate.Folders.Select(f => f.ToEntity(crate.Id)).ToList(),
            Files = crate.Files.Select(file => file.ToEntity(crate.Id)).ToList(),
            Members = crate.Members.Select(m => m.ToEntity(crate.Id)).ToList()
        };
    }

    public static Crate ToDomain(this CrateEntity entity)
    {
        return Crate.Rehydrate(
            entity.Id,
            entity.Name,
            entity.Color,
            StorageSize.FromBytes(entity.AllocatedStorageBytes),
            StorageSize.FromBytes(entity.UsedStorageBytes),
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.LastAccessedAt,
            entity.Folders.Select(f => f.ToDomain()).ToList(),
            entity.Files.Select(f => f.ToDomain()).ToList(),
            entity.Members.Select(m => m.ToDomain()).ToList()
        );
    }
    
    public static void UpdateEntity(this CrateEntity entity, Crate crate)
    {
        entity.Name = crate.Name;
        entity.Color = crate.Color;
        entity.AllocatedStorageBytes = crate.AllocatedStorage.Bytes;
        entity.UsedStorageBytes = crate.UsedStorage.Bytes;
        entity.UpdatedAt = crate.UpdatedAt;
    }
}