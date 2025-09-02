using CloudCrate.Application.DTOs.Folder.Response;
using CloudCrate.Domain.Entities;

namespace CloudCrate.Application.Common.Mappers;

public static class CrateFolderMapper
{
    public static CrateFolderResponse ToCrtaCrateFolderResponse(CrateFolder folder)
    {
        return new CrateFolderResponse()
        {
            Id = folder.Id,
            Name = folder.Name,
            CrateId = folder.CrateId,
            ParentFolderId = folder.ParentFolderId,
            Color = folder.Color,
            CreatedAt = folder.CreatedAt
        };
    }
}