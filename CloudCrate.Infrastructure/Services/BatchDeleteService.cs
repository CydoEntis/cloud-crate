using CloudCrate.Application.Errors;
using CloudCrate.Application.Extensions;
using CloudCrate.Application.Interfaces;
using CloudCrate.Application.Interfaces.Storage;
using CloudCrate.Application.Models;
using CloudCrate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

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

    public async Task<Result> DeleteCratesAsync(IEnumerable<Guid> crateIds,
        IDbContextTransaction? existingTransaction = null)
    {
        foreach (var batch in crateIds.Batch(BatchSize))
        {
            foreach (var crateId in batch)
            {
                var shouldManageTransaction = existingTransaction == null;
                var transaction = existingTransaction ?? await _context.Database.BeginTransactionAsync();

                try
                {
                    var storageResult = await _storageService.DeleteAllFilesForCrateAsync(crateId);
                    if (!storageResult.IsSuccess)
                    {
                        _logger.LogWarning("Failed to delete files for crate {CrateId}: {Error}",
                            crateId, storageResult.GetError().Message);
                    }

                    await _context.CrateFiles.Where(f => f.CrateId == crateId).ExecuteDeleteAsync();
                    await _context.CrateFolders.Where(f => f.CrateId == crateId).ExecuteDeleteAsync();
                    await _context.CrateMembers.Where(m => m.CrateId == crateId).ExecuteDeleteAsync();
                    await _context.Crates.Where(c => c.Id == crateId).ExecuteDeleteAsync();

                    if (shouldManageTransaction)
                    {
                        await transaction.CommitAsync();
                    }

                    _logger.LogInformation("Successfully deleted crate {CrateId}", crateId);
                }
                catch (Exception ex)
                {
                    if (shouldManageTransaction)
                    {
                        await transaction.RollbackAsync();
                    }

                    _logger.LogError(ex, "Failed to delete crate {CrateId}", crateId);
                    return Result.Failure(new InternalError($"Failed to delete crate {crateId}: {ex.Message}"));
                }
                finally
                {
                    if (shouldManageTransaction)
                    {
                        await transaction.DisposeAsync();
                    }
                }
            }
        }

        return Result.Success();
    }

    public async Task<Result> DeleteFilesAsync(IEnumerable<Guid> fileIds,
        IDbContextTransaction? existingTransaction = null)
    {
        var list = fileIds.ToList();

        while (list.Any())
        {
            var batch = list.Take(BatchSize).ToList();

            var shouldManageTransaction = existingTransaction == null;
            var transaction = existingTransaction ?? await _context.Database.BeginTransactionAsync();

            try
            {
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
                            "Failed to delete files for crate {CrateId}, folder {FolderId}: {Error}",
                            group.Key.CrateId, group.Key.CrateFolderId, result.GetError().Message);
                }

                await _context.CrateFiles.Where(f => batch.Contains(f.Id)).ExecuteDeleteAsync();

                if (shouldManageTransaction)
                {
                    await transaction.CommitAsync();
                }
            }
            catch (Exception ex)
            {
                if (shouldManageTransaction)
                {
                    await transaction.RollbackAsync();
                }

                _logger.LogError(ex, "Failed to delete file batch");
                return Result.Failure(new InternalError($"Failed to delete files: {ex.Message}"));
            }
            finally
            {
                if (shouldManageTransaction)
                {
                    await transaction.DisposeAsync();
                }
            }

            list = list.Skip(BatchSize).ToList();
        }

        return Result.Success();
    }

    public async Task<Result> DeleteFilesByFolderIdAsync(Guid folderId,
        IDbContextTransaction? existingTransaction = null)
    {
        var shouldManageTransaction = existingTransaction == null;
        var transaction = existingTransaction ?? await _context.Database.BeginTransactionAsync();

        try
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
                    _logger.LogWarning("Failed to delete files for crate {CrateId}, folder {FolderId}: {Error}",
                        group.Key, folderId, result.GetError().Message);
            }

            await _context.CrateFiles.Where(f => f.CrateFolderId == folderId).ExecuteDeleteAsync();

            if (shouldManageTransaction)
            {
                await transaction.CommitAsync();
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            if (shouldManageTransaction)
            {
                await transaction.RollbackAsync();
            }

            _logger.LogError(ex, "Failed to delete files for folder {FolderId}", folderId);
            return Result.Failure(new InternalError($"Failed to delete folder files: {ex.Message}"));
        }
        finally
        {
            if (shouldManageTransaction)
            {
                await transaction.DisposeAsync();
            }
        }
    }
}