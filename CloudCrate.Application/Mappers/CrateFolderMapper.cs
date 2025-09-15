using CloudCrate.Application.DTOs.Folder.Response;
using CloudCrate.Domain.Entities;

namespace CloudCrate.Application.Mappers;

public static class CrateFolderMapper
{
    public static CrateFolderResponse ToCrateFolderResponse(CrateFolder folder)
    {
        return new CrateFolderResponse
        {
            Id = folder.Id,
            Name = folder.Name,
            Color = folder.Color ?? "#EAAC00",
            ParentFolderId = folder.ParentFolderId,
            ParentFolderName = folder.ParentFolder?.Name,
            CrateId = folder.CrateId,
            CreatedAt = folder.CreatedAt,
            UpdatedAt = folder.UpdatedAt,
            IsDeleted = folder.IsDeleted
        };
    }

    public static FolderResponse ToFolderResponse(CrateFolder folder)
    {
        return new FolderResponse
        {
            Id = folder.Id,
            Name = folder.Name,
            CrateId = folder.CrateId,
            ParentFolderId = folder.ParentFolderId,
            Color = folder.Color,
            UploadedByUserId = folder.CreatedByUserId,
            UploadedByDisplayName = folder.CreatedByUser?.DisplayName ?? "Unknown",
            UploadedByEmail = folder.CreatedByUser?.Email ?? "",
            UploadedByProfilePictureUrl = folder.CreatedByUser?.ProfilePictureUrl ?? "",
            CreatedAt = folder.CreatedAt
        };
    }
}