using CloudCrate.Application.DTOs.File;
using CloudCrate.Application.DTOs.User;
using CloudCrate.Application.DTOs.User.Mappers;
using CloudCrate.Application.DTOs.User.Response;
using CloudCrate.Domain.Entities;

namespace CloudCrate.Application.Common.Mappings;

public static class FileMapper
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

    public static CrateFileResponse ToCrateFileResponse(CrateFile file, string fileUrl)
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
            Uploader = UserMapper.ToUploader(file.Uploader),
            CreatedAt = file.CreatedAt,
        };
    }
}