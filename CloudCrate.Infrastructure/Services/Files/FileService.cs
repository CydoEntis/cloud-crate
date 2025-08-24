using System.Transactions;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs;
using CloudCrate.Application.Interfaces.Bulk;
using CloudCrate.Application.Interfaces.File;
using CloudCrate.Application.Interfaces.Folder;
using CloudCrate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CloudCrate.Infrastructure.Services.Files;

public class BulkService : IBulkService
{
    private readonly IFileService _fileService;
    private readonly IFolderService _folderService;
    private readonly AppDbContext _context;

    public BulkService(IFileService fileService, IFolderService folderService, AppDbContext context)
    {
        _fileService = fileService;
        _folderService = folderService;
        _context = context;
    }

    public async Task<Result> MoveAsync(MultipleMoveRequest request, string userId)
    {
        if (request.NewParentId.HasValue && request.NewParentId.Value == Guid.Empty)
            request.NewParentId = null;

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

        // Exclude files already inside moved folders
        var nestedFileIds = await _context.FileObjects
            .Where(f => f.FolderId.HasValue && request.FolderIds.Contains(f.FolderId.Value))
            .Select(f => f.Id)
            .ToListAsync();

        var topLevelFileIds = request.FileIds.Except(nestedFileIds).ToList();

        if (topLevelFileIds.Any())
        {
            var filesResult = await _fileService.MoveFilesAsync(topLevelFileIds, request.NewParentId, userId);
            if (!filesResult.Succeeded) return filesResult;
        }

        foreach (var folderId in request.FolderIds)
        {
            var res = await _folderService.MoveFolderAsync(folderId, request.NewParentId, userId);
            if (!res.Succeeded) return res;
        }

        scope.Complete();
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(MultipleDeleteRequest request, string userId)
    {
        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

        // Exclude files already inside deleted folders
        var nestedFileIds = await _context.FileObjects
            .Where(f => f.FolderId.HasValue && request.FolderIds.Contains(f.FolderId.Value))
            .Select(f => f.Id)
            .ToListAsync();

        var topLevelFileIds = request.FileIds.Except(nestedFileIds).ToList();

        if (topLevelFileIds.Any())
        {
            var fileResult = await _fileService.SoftDeleteFilesAsync(topLevelFileIds, userId);
            if (!fileResult.Succeeded) return fileResult;
        }

        foreach (var folderId in request.FolderIds)
        {
            var res = await _folderService.SoftDeleteFolderAsync(folderId, userId);
            if (!res.Succeeded) return res;
        }

        scope.Complete();
        return Result.Success();
    }

    public async Task<Result> RestoreAsync(MultipleRestoreRequest request, string userId)
    {
        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

        // Exclude files already inside restored folders
        var nestedFileIds = await _context.FileObjects
            .Where(f => f.FolderId.HasValue && request.FolderIds.Contains(f.FolderId.Value))
            .Select(f => f.Id)
            .ToListAsync();

        var topLevelFileIds = request.FileIds.Except(nestedFileIds).ToList();

        foreach (var folderId in request.FolderIds)
        {
            var res = await _folderService.RestoreFolderAsync(folderId, userId);
            if (!res.Succeeded) return res;
        }

        foreach (var fileId in topLevelFileIds)
        {
            var res = await _fileService.RestoreFileAsync(fileId, userId);
            if (!res.Succeeded) return res;
        }

        scope.Complete();
        return Result.Success();
    }
}