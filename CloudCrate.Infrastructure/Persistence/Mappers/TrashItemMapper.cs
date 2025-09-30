using CloudCrate.Application.DTOs.Trash;
using CloudCrate.Infrastructure.Persistence.Entities;

namespace CloudCrate.Infrastructure.Persistence.Mappers;

public static class TrashItemMapper
{
    public static TrashItemResponse ToTrashItemResponse(
        CrateFileEntity file,
        bool canModify)
    {
        return new TrashItemResponse
        {
            Id = file.Id,
            Name = file.Name,
            Type = TrashItemType.File,
            SizeInBytes = file.SizeInBytes,
            DeletedAt = file.DeletedAt ?? DateTime.MinValue,
            DeletedByUserId = file.DeletedByUserId ?? string.Empty,
            DeletedByUserName = file.DeletedByUser?.DisplayName ?? "Unknown",
            CreatedByUserId = file.UploadedByUserId,
            CreatedByUserName = file.UploadedByUser?.DisplayName ?? "Unknown",
            CanRestore = canModify,
            CanPermanentlyDelete = canModify,
            CrateId = file.CrateId,
            CrateName = file.Crate.Name
        };
    }

    public static TrashItemResponse ToTrashItemResponse(
        CrateFolderEntity folder,
        bool canModify)
    {
        return new TrashItemResponse
        {
            Id = folder.Id,
            Name = folder.Name,
            Type = TrashItemType.Folder,
            SizeInBytes = null,
            DeletedAt = folder.DeletedAt ?? DateTime.MinValue,
            DeletedByUserId = folder.DeletedByUserId ?? string.Empty,
            DeletedByUserName = folder.DeletedByUser?.DisplayName ?? "Unknown",
            CreatedByUserId = folder.CreatedByUserId,
            CreatedByUserName = folder.CreatedByUser?.DisplayName ?? "Unknown",
            CanRestore = canModify,
            CanPermanentlyDelete = canModify,
            CrateId = folder.CrateId,
            CrateName = folder.Crate.Name
        };
    }

    public static List<TrashItemResponse> ToTrashItemResponses(
        List<CrateFileEntity> files,
        List<CrateFolderEntity> folders,
        string userId)
    {
        var trashItems = new List<TrashItemResponse>(files.Count + folders.Count);
        
        foreach (var file in files)
        {
            var canModify = file.UploadedByUserId == userId || 
                           file.DeletedByUserId == userId;
            
            trashItems.Add(ToTrashItemResponse(file, canModify));
        }
        
        foreach (var folder in folders)
        {
            var canModify = folder.CreatedByUserId == userId || 
                           folder.DeletedByUserId == userId;
            
            trashItems.Add(ToTrashItemResponse(folder, canModify));
        }
        
        return trashItems;
    }
}