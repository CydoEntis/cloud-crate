using CloudCrate.Application.DTOs.Folder.Response;
using CloudCrate.Application.DTOs.User.Response;
using CloudCrate.Domain.Entities;
using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.Common.Mappings;

public static class FolderMappings
{
    public static async Task<FolderOrFileItem> ToFolderOrFileItemAsync(
        this Folder folder,
        List<UserResponse> users,
        Func<Guid, Task<long>> getFolderSize = null)
    {
        var uploader = !string.IsNullOrWhiteSpace(folder.UploadedByUserId)
            ? users.FirstOrDefault(u => u.Id == folder.UploadedByUserId)
            : null;

        var size = getFolderSize != null ? await getFolderSize(folder.Id) : 0;

        return new FolderOrFileItem
        {
            Id = folder.Id,
            Name = folder.Name,
            Type = FolderItemType.Folder,
            CrateId = folder.CrateId,
            ParentFolderId = folder.ParentFolderId,
            Color = folder.Color,
            SizeInBytes = size,
            UploadedByUserId = folder.UploadedByUserId ?? string.Empty,
            UploadedByDisplayName = folder.UploadedByDisplayName ?? uploader?.DisplayName ?? "Unknown",
            UploadedByEmail = folder.UploadedByEmail ?? uploader?.Email ?? string.Empty,
            UploadedByProfilePictureUrl =
                folder.UploadedByProfilePictureUrl ?? uploader?.ProfilePictureUrl ?? string.Empty,
            CreatedAt = folder.CreatedAt,
        };
    }

    public static FolderOrFileItem ToFolderOrFileItem(this FileObject file, List<UserResponse> users)
    {
        var uploader = users.FirstOrDefault(u => u.Id == file.UploadedByUserId);

        return new FolderOrFileItem
        {
            Id = file.Id,
            Name = file.Name,
            Type = FolderItemType.File,
            MimeType = file.MimeType,
            SizeInBytes = file.SizeInBytes,
            CrateId = file.CrateId,
            ParentFolderId = file.FolderId,
            Color = null,
            UploadedByUserId = file.UploadedByUserId ?? string.Empty,
            UploadedByDisplayName = uploader?.DisplayName ?? "Unknown",
            UploadedByEmail = uploader?.Email ?? string.Empty,
            UploadedByProfilePictureUrl = uploader?.ProfilePictureUrl ?? string.Empty,
            CreatedAt = file.CreatedAt
        };
    }
}