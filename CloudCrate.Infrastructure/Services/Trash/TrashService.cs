using CloudCrate.Application.DTOs.Pagination;
using CloudCrate.Application.DTOs.Trash;
using CloudCrate.Application.Extensions;
using CloudCrate.Application.Interfaces.Trash;
using CloudCrate.Application.Interfaces.File;
using CloudCrate.Application.Interfaces.Folder;
using CloudCrate.Application.Interfaces.Persistence;
using CloudCrate.Application.Interfaces.Permissions;
using CloudCrate.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace CloudCrate.Infrastructure.Services.Trash;

public class TrashService : ITrashService
{
    private readonly IAppDbContext _context;
    private readonly IFileService _fileService;
    private readonly IFolderService _folderService;
    private readonly ICrateRoleService _crateRoleService;

    public TrashService(
        IAppDbContext context,
        IFileService fileService,
        IFolderService folderService,
        ICrateRoleService crateRoleService)
    {
        _context = context;
        _fileService = fileService;
        _folderService = folderService;
        _crateRoleService = crateRoleService;
    }

    public async Task<PaginatedResult<TrashItemResponse>> FetchDeletedItemsAsync(
        Guid crateId, string userId, int page, int pageSize)
    {
        var canView = await _crateRoleService.CanView(crateId, userId);
        if (canView.IsFailure)
            throw new UnauthorizedAccessException("User does not have permission to view crate trash");

        var fileItems = await _context.CrateFiles
            .Where(f => f.CrateId == crateId && f.IsDeleted)
            .Select(f => new TrashItemResponse
            {
                Id = f.Id,
                Name = f.Name,
                IsFolder = false,
                DeletedAt = f.DeletedAt ?? f.CreatedAt
            })
            .ToListAsync();

        var folderItems = await _context.CrateFolders
            .Where(f => f.CrateId == crateId && f.IsDeleted)
            .Select(f => new TrashItemResponse
            {
                Id = f.Id,
                Name = f.Name,
                IsFolder = true,
                DeletedAt = f.DeletedAt ?? f.CreatedAt
            })
            .ToListAsync();

        var combined = fileItems.Concat(folderItems)
            .OrderByDescending(x => x.DeletedAt)
            .ToList();

        return combined.ToPaginatedResult(page, pageSize);
    }


    public async Task<Result> RestoreItemsAsync(List<Guid> fileIds, List<Guid> folderIds, string userId)
    {
        foreach (var folderId in folderIds)
        {
            var result = await _folderService.RestoreFolderAsync(folderId, userId);
            if (result.IsFailure) return result;
        }

        foreach (var fileId in fileIds)
        {
            var result = await _fileService.RestoreFileAsync(fileId, userId);
            if (result.IsFailure) return result;
        }

        return Result.Success();
    }

    public async Task<Result> PermanentlyDeleteItemsAsync(List<Guid> fileIds, List<Guid> folderIds, string userId)
    {
        foreach (var folderId in folderIds)
        {
            var result = await _folderService.PermanentlyDeleteFolderAsync(folderId, userId);
            if (result.IsFailure) return result;
        }

        foreach (var fileId in fileIds)
        {
            var result = await _fileService.DeleteFileAsync(fileId, userId);
            if (result.IsFailure) return result;
        }

        return Result.Success();
    }
}