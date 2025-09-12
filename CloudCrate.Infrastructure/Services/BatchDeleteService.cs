using CloudCrate.Application.Extensions;
using CloudCrate.Application.Interfaces;
using CloudCrate.Application.Interfaces.Storage;
using CloudCrate.Application.Models;
using CloudCrate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CloudCrate.Infrastructure.Services;

public class BatchDeleteService : IBatchDeleteService
{
    private readonly AppDbContext _context;
    private readonly IStorageService _storageService;
    private readonly ILogger<BatchDeleteService> _logger;
    private const int BatchSize = 500;

    public BatchDeleteService(
        AppDbContext context,
        IStorageService storageService,
        ILogger<BatchDeleteService> logger)
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

            foreach (var group in files.GroupBy(f => new { f.CrateId, f.CrateFolderId }))
            {
                var result = await _storageService.DeleteFilesAsync(
                    group.Key.CrateId, group.Key.CrateFolderId, group.Select(f => f.Name));

                if (!result.IsSuccess)
                    _logger.LogWarning(
                        "Failed to delete files for crate {CrateId}, folder {FolderId}",
                        group.Key.CrateId, group.Key.CrateFolderId);
            }

            // Remove from DB
            await _context.CrateFiles.Where(f => batch.Contains(f.Id)).ExecuteDeleteAsync();
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
                var result = await _storageService.DeleteAllFilesForCrateAsync(crateId);
                if (!result.IsSuccess)
                    _logger.LogWarning("Failed to delete files for crate {CrateId}", crateId);

                await _context.CrateFiles.Where(f => f.CrateId == crateId).ExecuteDeleteAsync();
                await _context.CrateFolders.Where(f => f.CrateId == crateId).ExecuteDeleteAsync();
                await _context.CrateMembers.Where(m => m.CrateId == crateId).ExecuteDeleteAsync();
                await _context.Crates.Where(c => c.Id == crateId).ExecuteDeleteAsync();
            }
        }

        return Result.Success();
    }


    public async Task<Result> DeleteFilesByFolderIdAsync(Guid folderId)
    {
        var files = await _context.CrateFiles
            .Where(f => f.CrateFolderId == folderId)
            .Select(f => new { f.Id, f.CrateId, f.Name })
            .ToListAsync();

        foreach (var group in files.GroupBy(f => f.CrateId))
        {
            var result = await _storageService.DeleteFilesAsync(
                group.Key, folderId, group.Select(f => f.Name));

            if (!result.IsSuccess)
                _logger.LogWarning("Failed to delete files for crate {CrateId}, folder {FolderId}",
                    group.Key, folderId);
        }

        await _context.CrateFiles.Where(f => f.CrateFolderId == folderId).ExecuteDeleteAsync();
        return Result.Success();
    }
}
