using CloudCrate.Application.DTOs.File.Response;
using CloudCrate.Application.Utils;
using CloudCrate.Domain.Entities;

namespace CloudCrate.Application.Common.Utils;

public static class FileBreakdownHelper
{
    public static List<FileTypeBreakdownResponse> GetFilesByMimeTypeInMemory(IEnumerable<CrateFile> files)
    {
        var activeFiles = files.Where(f => !f.IsDeleted);
        var deletedFiles = files.Where(f => f.IsDeleted);

        var breakdown = activeFiles
            .GroupBy(f => MimeCategoryHelper.GetMimeCategory(f.MimeType))
            .Select(g => new FileTypeBreakdownResponse
            {
                Type = g.Key,
                SizeMb = Math.Round(g.Sum(f => (long?)f.Size.Bytes ?? 0) / 1024.0 / 1024.0, 2)
            })
            .ToList();

        if (deletedFiles.Any())
        {
            breakdown.Add(new FileTypeBreakdownResponse
            {
                Type = "Trash",
                SizeMb = Math.Round(deletedFiles.Sum(f => (long?)f.Size.Bytes ?? 0) / 1024.0 / 1024.0, 2)
            });
        }

        return breakdown;
    }
}