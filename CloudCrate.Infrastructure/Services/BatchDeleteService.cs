using CloudCrate.Application.Extensions;
using CloudCrate.Application.Interfaces;
using CloudCrate.Application.Interfaces.Persistence;
using CloudCrate.Application.Interfaces.Storage;
using CloudCrate.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CloudCrate.Infrastructure.Services;

public class BatchDeleteService : IBatchDeleteService
{
    private readonly IAppDbContext _context;
    private readonly IStorageService _storageService;
    private readonly ILogger<BatchDeleteService> _logger;
    private const int BatchSize = 500;

    public BatchDeleteService(IAppDbContext context, IStorageService storageService, ILogger<BatchDeleteService> logger)
    {
        _context = context;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<Result> DeleteFilesAsync(IEnumerable<Guid> fileIds)
    {
        var list = fileIds.ToList();
        while (list.Any())
        {
            var batch = list.Take(BatchSize).ToList();

            var files = await _context.CrateFiles
                .Where(f => batch.Contains(f.Id))
                .Select(f => new { f.Id, f.CrateId, f.CrateFolderId, f.Name })
                .ToListAsync();

            var groupedByCrate = files.GroupBy(f => new { f.CrateId, f.CrateFolderId });

            foreach (var group in groupedByCrate)
            {
                var result = await _storageService.DeleteFilesAsync(
                    "system", group.Key.CrateId, group.Key.CrateFolderId, group.Select(f => f.Name));
                if (!result.IsSuccess)
                    _logger.LogWarning("Failed to delete files in storage for crate {CrateId}, folder {FolderId}",
                        group.Key.CrateId, group.Key.CrateFolderId);
            }

            await _context.CrateFiles.Where(f => batch.Contains(f.Id)).ExecuteDeleteAsync();
            list = list.Skip(BatchSize).ToList();
        }

        return Result.Success();
    }

    public async Task<Result> DeleteFoldersAsync(IEnumerable<Guid> folderIds)
    {
        var list = folderIds.ToList();
        while (list.Any())
        {
            var batch = list.Take(BatchSize).ToList();

            var folders = await _context.CrateFolders
                .Where(f => batch.Contains(f.Id))
                .Select(f => new { f.Id, f.CrateId })
                .ToListAsync();

            var groupedByCrate = folders.GroupBy(f => f.CrateId);

            foreach (var group in groupedByCrate)
            {
                var result = await _storageService.DeleteFoldersAsync("system", group.Key, group.Select(f => f.Id));
                if (!result.IsSuccess)
                    _logger.LogWarning("Failed to delete folders in storage for crate {CrateId}", group.Key);
            }

            await _context.CrateFolders.Where(f => batch.Contains(f.Id)).ExecuteDeleteAsync();
            list = list.Skip(BatchSize).ToList();
        }

        return Result.Success();
    }

    public async Task<Result> DeleteCratesAsync(IEnumerable<Guid> crateIds)
    {
        foreach (var batch in crateIds.Batch(BatchSize))
        {
            foreach (var crateId in batch)
            {
                var bucketResult = await _storageService.DeleteBucketAsync(crateId);
                if (!bucketResult.IsSuccess)
                    _logger.LogWarning("Failed to delete bucket for crate {CrateId}", crateId);

                await DeleteFilesByCrateIdsAsync(new[] { crateId });
                await DeleteFoldersByCrateIdsAsync(new[] { crateId });
                await DeleteMembersByCrateIdsAsync(new[] { crateId });

                await _context.Crates.Where(c => c.Id == crateId).ExecuteDeleteAsync();
            }
        }

        return Result.Success();
    }


    private async Task DeleteFilesByCrateIdsAsync(IEnumerable<Guid> crateIds)
    {
        foreach (var batch in crateIds.Batch(BatchSize))
        {
            var files = await _context.CrateFiles
                .Where(f => batch.Contains(f.CrateId))
                .Select(f => new { f.CrateId, f.CrateFolderId, f.Name })
                .ToListAsync();

            foreach (var group in files.GroupBy(f => new { f.CrateId, f.CrateFolderId }))
            {
                var result = await _storageService.DeleteFilesAsync(
                    "system", group.Key.CrateId, group.Key.CrateFolderId, group.Select(f => f.Name));
                if (!result.IsSuccess)
                    _logger.LogWarning("Failed to delete files in storage for crate {CrateId}, folder {FolderId}",
                        group.Key.CrateId, group.Key.CrateFolderId);
            }

            await _context.CrateFiles.Where(f => batch.Contains(f.CrateId)).ExecuteDeleteAsync();
        }
    }

    private async Task DeleteFoldersByCrateIdsAsync(IEnumerable<Guid> crateIds)
    {
        foreach (var batch in crateIds.Batch(BatchSize))
        {
            var folders = await _context.CrateFolders
                .Where(f => batch.Contains(f.CrateId))
                .Select(f => new { f.Id, f.CrateId })
                .ToListAsync();

            foreach (var group in folders.GroupBy(f => f.CrateId))
            {
                var result = await _storageService.DeleteFoldersAsync("system", group.Key, group.Select(f => f.Id));
                if (!result.IsSuccess)
                    _logger.LogWarning("Failed to delete folders in storage for crate {CrateId}", group.Key);
            }

            await _context.CrateFolders.Where(f => batch.Contains(f.CrateId)).ExecuteDeleteAsync();
        }
    }

    private async Task DeleteMembersByCrateIdsAsync(IEnumerable<Guid> crateIds)
    {
        foreach (var batch in crateIds.Batch(BatchSize))
        {
            await _context.CrateMembers.Where(m => batch.Contains(m.CrateId)).ExecuteDeleteAsync();
        }
    }
}