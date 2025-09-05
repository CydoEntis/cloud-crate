using CloudCrate.Application.DTOs.File;
using CloudCrate.Application.DTOs.User;

namespace CloudCrate.Application.Mappers;

public static class CrateFileMapper
{
    public static SoftDeletedFile MapToSoftDeletedFile(CrateFile file)
    {
        return new SoftDeletedFile
        {
            Id = file.Id,
            Name = file.Name,
            SizeInBytes = file.SizeInBytes,
            MimeType = file.MimeType,
            IsDeleted = file.IsDeleted,
            DeletedAt = file.DeletedAt
        };
    }

    public static CrateFileResponse ToCrateFileResponse(CrateFile file, Uploader uploader, string fileUrl)
    {
        return new CrateFileResponse
        {
            Id =  file.Id,
            Name = file.Name,
            SizeInBytes = file.SizeInBytes,
            MimeType = file.MimeType,
            FileUrl = fileUrl,
            IsDeleted = file.IsDeleted,
            CrateId = file.CrateId,
            FolderId = file.CrateFolderId ?? null,
            FolderName = file.CrateFolder?.Name,
            Uploader = uploader,
            CreatedAt = file.CreatedAt,
        };
    }
}