using CloudCrate.Application.Models;
using Microsoft.EntityFrameworkCore.Storage;


namespace CloudCrate.Application.Interfaces;

public interface IBatchDeleteService
{
    Task<Result> DeleteCratesAsync(IEnumerable<Guid> crateIds, IDbContextTransaction? existingTransaction = null);
    Task<Result> DeleteFilesAsync(IEnumerable<Guid> fileIds, IDbContextTransaction? existingTransaction = null);
    Task<Result> DeleteFilesByFolderIdAsync(Guid folderId, IDbContextTransaction? existingTransaction = null);
}