using CloudCrate.Application.DTOs.File;
using CloudCrate.Application.DTOs.File.Response;
using CloudCrate.Application.DTOs.User.Projections;
using CloudCrate.Domain.Entities;

namespace CloudCrate.Application.Mappers;

public static class CrateFileDomainMapper
{
    public static CrateFileResponse ToCrateFileResponse(CrateFile file, Uploader uploader, string fileUrl)
    {
        return new CrateFileResponse
        {
            Id = file.Id,
            Name = file.Name,
            SizeInBytes = file.Size.Bytes,
            MimeType = file.MimeType,
            FileUrl = fileUrl,
            IsDeleted = file.IsDeleted,
            CrateId = file.CrateId,
            FolderId = file.CrateFolderId ?? null,
            FolderName = file.Folder?.Name,
            Uploader = uploader,
            CreatedAt = file.CreatedAt,
        };
    }
}