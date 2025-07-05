using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Interfaces;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.Folder;
using CloudCrate.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CloudCrate.Application.Services;

public class FolderService : IFolderService
{
    private readonly IAppDbContext _context;

    public FolderService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<Result<FolderResponse>> CreateFolderAsync(CreateFolderRequest request, string userId)
    {
        var crate = await _context.Crates
            .FirstOrDefaultAsync(c => c.Id == request.CrateId && c.UserId == userId);

        if (crate == null)
            return Result<FolderResponse>.Failure(Errors.CrateNotFound);

        if (request.ParentFolderId.HasValue)
        {
            var parent = await _context.Folders
                .FirstOrDefaultAsync(f => f.Id == request.ParentFolderId && f.Crate.UserId == userId);

            if (parent == null)
                return Result<FolderResponse>.Failure(Errors.FolderNotFound);
        }

        var folder = new Folder
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            CrateId = request.CrateId,
            ParentFolderId = request.ParentFolderId,
            Color = request.Color,
        };

        _context.Folders.Add(folder);
        await _context.SaveChangesAsync();

        return Result<FolderResponse>.Success(new FolderResponse
        {
            Id = folder.Id,
            Name = folder.Name,
            CrateId = folder.CrateId,
            ParentFolderId = folder.ParentFolderId
        });
    }

    public async Task<Result> RenameFolderAsync(Guid folderId, string newName, string userId)
    {
        var folder = await _context.Folders
            .Include(f => f.Crate)
            .FirstOrDefaultAsync(f => f.Id == folderId && f.Crate.UserId == userId);

        if (folder == null)
            return Result.Failure(Errors.FolderNotFound);

        folder.Name = newName;
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    // TODO: Potentially make it so when a folder is deleted, it deletes all the contents of that folder.
    public async Task<Result> DeleteFolderAsync(Guid folderId, string userId)
    {
        var folder = await _context.Folders
            .Include(f => f.Crate)
            .Include(f => f.Subfolders)
            .Include(f => f.Files)
            .FirstOrDefaultAsync(f => f.Id == folderId && f.Crate.UserId == userId);

        if (folder == null)
            return Result.Failure(Errors.FolderNotFound);

        if (folder.Subfolders.Any() || folder.Files.Any())
            return Result.Failure(Errors.FolderNotEmpty);

        _context.Folders.Remove(folder);
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<List<FolderResponse>>> GetRootFoldersAsync(Guid crateId, string userId)
    {
        var folders = await _context.Folders
            .Where(f => f.CrateId == crateId && f.ParentFolderId == null && f.Crate.UserId == userId)
            .Select(f => new FolderResponse
            {
                Id = f.Id,
                Name = f.Name,
                CrateId = f.CrateId,
                ParentFolderId = f.ParentFolderId,
                Color = f.Color
            })
            .ToListAsync();

        return Result<List<FolderResponse>>.Success(folders);
    }

    public async Task<Result<List<FolderResponse>>> GetSubfoldersAsync(Guid parentId, string userId)
    {
        var subfolders = await _context.Folders
            .Where(f => f.ParentFolderId == parentId && f.Crate.UserId == userId)
            .Select(f => new FolderResponse
            {
                Id = f.Id,
                Name = f.Name,
                CrateId = f.CrateId,
                ParentFolderId = f.ParentFolderId,
                Color = f.Color
            })
            .ToListAsync();

        return Result<List<FolderResponse>>.Success(subfolders);
    }

    public async Task<Result> MoveFolderAsync(Guid folderId, Guid? newParentId, string userId)
    {
        var folder = await _context.Folders
            .Include(f => f.Crate)
            .FirstOrDefaultAsync(f => f.Id == folderId && f.Crate.UserId == userId);

        if (folder == null)
            return Result.Failure(Errors.FolderNotFound);

        if (newParentId.HasValue)
        {
            var newParent = await _context.Folders
                .Include(f => f.Crate)
                .FirstOrDefaultAsync(f => f.Id == newParentId.Value && f.Crate.UserId == userId);

            if (newParent == null)
                return Result.Failure(Errors.FolderNotFound);

            if (newParentId == folder.Id)
                return Result.Failure(Errors.InvalidMove);

            Guid? currentParentId = newParentId;
            while (currentParentId != null)
            {
                if (currentParentId == folder.Id)
                    return Result.Failure(Errors.InvalidMove);

                currentParentId = await _context.Folders
                    .Where(f => f.Id == currentParentId)
                    .Select(f => f.ParentFolderId)
                    .FirstOrDefaultAsync();
            }
        }

        folder.ParentFolderId = newParentId;
        await _context.SaveChangesAsync();

        return Result.Success();
    }
}