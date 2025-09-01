using CloudCrate.Application.DTOs.File;
using CloudCrate.Application.DTOs.Folder.Response;
using CloudCrate.Application.DTOs.User.Response;
using CloudCrate.Domain.Entities;
using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.Common.Mappings;

public static class FolderItemMapper
{
    // public static FolderOrFileItem MapFile(FileItemDto file)
    // {
    //     return new FolderOrFileItem
    //     {
    //         Id = file.Id,
    //         Name = file.Name,
    //         Type = FolderItemType.File,
    //         CrateId = file.CrateId,
    //         ParentFolderId = file.ParentFolderId,
    //         ParentFolderName = file.ParentFolderName,
    //         MimeType = file.MimeType,
    //         SizeInBytes = file.SizeInBytes,
    //         UploadedByUserId = file.UploadedByUserId,
    //         UploadedByDisplayName = file.UploadedByDisplayName,
    //         UploadedByEmail = file.UploadedByEmail,
    //         UploadedByProfilePictureUrl = file.UploadedByProfilePictureUrl,
    //         CreatedAt = file.CreatedAt,
    //         FileUrl = file.FileUrl ?? string.Empty
    //     };
    // }

    // public static async Task<FolderOrFileItem> MapFolderAsync(
    //     Folder folder,
    //     IEnumerable<UserResponse> uploaders,
    //     Func<Guid, Task<long>> getFolderSizeAsync)
    // {
    //     var uploader = uploaders.FirstOrDefault(u => u.Id == folder.UploadedByUserId);
    //
    //     var size = await getFolderSizeAsync(folder.Id);
    //
    //     return new FolderOrFileItem
    //     {
    //         Id = folder.Id,
    //         Name = folder.Name,
    //         Type = FolderItemType.Folder,
    //         CrateId = folder.CrateId,
    //         ParentFolderId = folder.ParentFolderId,
    //         ParentFolderName = null,
    //         MimeType = null,
    //         SizeInBytes = size,
    //         Color = folder.Color,
    //         UploadedByUserId = uploader?.Id ?? folder.UploadedByUserId,
    //         UploadedByDisplayName = uploader?.DisplayName ?? folder.UploadedByDisplayName,
    //         UploadedByEmail = uploader?.Email ?? folder.UploadedByEmail,
    //         UploadedByProfilePictureUrl = uploader?.ProfilePictureUrl ?? folder.UploadedByProfilePictureUrl,
    //         CreatedAt = folder.CreatedAt,
    //         FileUrl = null
    //     };
    // }

    public static FolderResponse MapFolderResponse(Folder folder)
    {
        return new FolderResponse
        {
            Id = folder.Id,
            Name = folder.Name,
            CrateId = folder.CrateId,
            ParentFolderId = folder.ParentFolderId,
            Color = folder.Color,
            UploadedByUserId = folder.UploadedByUserId,
            UploadedByDisplayName = folder.UploadedByDisplayName,
            UploadedByEmail = folder.UploadedByEmail,
            UploadedByProfilePictureUrl = folder.UploadedByProfilePictureUrl,
            CreatedAt = folder.CreatedAt
        };
    }
}