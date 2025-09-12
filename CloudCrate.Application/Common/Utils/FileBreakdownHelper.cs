using CloudCrate.Application.DTOs.File;
using CloudCrate.Application.Common.Utils;
using CloudCrate.Application.DTOs.File.Request;
using CloudCrate.Domain.Entities;

namespace CloudCrate.Application.Common.Utils;

public static class FileBreakdownHelper
{
    public static List<FileTypeBreakdownDto> GetFilesByMimeTypeInMemory(IEnumerable<CrateFile> files)
    {
        return files
            .GroupBy(f => MimeCategoryHelper.GetMimeCategory(f.MimeType))
            .Select(g => new FileTypeBreakdownDto
            {
                Type = g.Key,
                SizeMb = Math.Round(g.Sum(f => (long?)f.Size.Bytes ?? 0) / 1024.0 / 1024.0, 2)
            })
            .ToList();
    }
}