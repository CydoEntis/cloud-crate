using CloudCrate.Application.Models;


namespace CloudCrate.Application.Interfaces;

public interface IBatchDeleteService
{
    Task<Result> DeleteFilesAsync(IEnumerable<Guid> fileIds);
    Task<Result> DeleteFoldersAsync(IEnumerable<Guid> folderIds);
    Task<Result> DeleteCratesAsync(IEnumerable<Guid> crateIds);
}