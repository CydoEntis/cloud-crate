﻿using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.Folder.Request;
using CloudCrate.Application.DTOs.Folder.Response;
using CloudCrate.Application.Interfaces.Folder;
using CloudCrate.Application.Interfaces.Persistence;
using CloudCrate.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CloudCrate.Infrastructure.Services.Folder;

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
            return Result<FolderResponse>.Failure(Errors.Crates.NotFound);

        if (request.ParentFolderId.HasValue)
        {
            var parent = await _context.Folders
                .FirstOrDefaultAsync(f => f.Id == request.ParentFolderId && f.Crate.UserId == userId);

            if (parent == null)
                return Result<FolderResponse>.Failure(Errors.Folders.NotFound);
        }

        var folder = Domain.Entities.Folder.Create(
            request.Name,
            request.CrateId,
            request.ParentFolderId,
            request.Color
        );

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
            return Result.Failure(Errors.Folders.NotFound);

        folder.Name = newName;
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result> DeleteFolderAsync(Guid folderId, string userId)
    {
        var folder = await _context.Folders
            .Include(f => f.Crate)
            .Include(f => f.Subfolders)
            .Include(f => f.Files)
            .FirstOrDefaultAsync(f => f.Id == folderId && f.Crate.UserId == userId);

        if (folder == null)
            return Result.Failure(Errors.Folders.NotFound);

        if (folder.Subfolders.Any() || folder.Files.Any())
            return Result.Failure(Errors.Folders.NotEmpty);

        _context.Folders.Remove(folder);
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result> MoveFolderAsync(Guid folderId, Guid? newParentId, string userId)
    {
        var folder = await _context.Folders
            .Include(f => f.Crate)
            .FirstOrDefaultAsync(f => f.Id == folderId && f.Crate.UserId == userId);

        if (folder == null)
            return Result.Failure(Errors.Folders.NotFound);

        if (newParentId.HasValue)
        {
            var newParent = await _context.Folders
                .Include(f => f.Crate)
                .FirstOrDefaultAsync(f => f.Id == newParentId.Value && f.Crate.UserId == userId);

            if (newParent == null)
                return Result.Failure(Errors.Folders.NotFound);

            if (newParentId == folder.Id)
                return Result.Failure(Errors.Folders.InvalidMove);

            Guid? currentParentId = newParentId;
            while (currentParentId != null)
            {
                if (currentParentId == folder.Id)
                    return Result.Failure(Errors.Folders.InvalidMove);

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

    public async Task<Result<FolderContentsResponse>> GetFolderContentsAsync(
        Guid crateId,
        Guid? parentFolderId,
        string userId,
        string? search,
        int page = 1,
        int pageSize = 20
    )
    {
        var crateExists = await _context.Crates
            .AnyAsync(c => c.Id == crateId && c.UserId == userId);

        if (!crateExists)
            return Result<FolderContentsResponse>.Failure(Errors.Crates.NotFound);

        // Folders
        var foldersQuery = _context.Folders
            .Where(f => f.CrateId == crateId &&
                        f.ParentFolderId == parentFolderId &&
                        f.Crate.UserId == userId);

        // Files
        var filesQuery = _context.FileObjects
            .Where(f => f.CrateId == crateId &&
                        f.FolderId == parentFolderId &&
                        f.Crate.UserId == userId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            foldersQuery = foldersQuery.Where(f => EF.Functions.ILike(f.Name, $"%{search}%"));
            filesQuery = filesQuery.Where(f => EF.Functions.ILike(f.Name, $"%{search}%"));
        }

        var folders = await foldersQuery
            .Select(f => new FolderOrFileItem
            {
                Id = f.Id,
                Name = f.Name,
                Type = FolderItemType.Folder,
                CrateId = f.CrateId,
                ParentFolderId = f.ParentFolderId,
                Color = f.Color
            })
            .ToListAsync();

        var files = await filesQuery
            .Select(f => new FolderOrFileItem
            {
                Id = f.Id,
                Name = f.Name,
                Type = FolderItemType.File,
                MimeType = f.MimeType,
                SizeInBytes = f.SizeInBytes,
                CrateId = f.CrateId,
                ParentFolderId = f.FolderId,
                Color = null
            })
            .ToListAsync();

        // Merge and sort
        var combined = folders
            .Concat(files)
            .OrderBy(i => i.Type)
            .ThenBy(i => i.Name)
            .ToList();

        // Pagination
        var totalCount = combined.Count;
        var pagedItems = combined
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // Parent-of-parent for virtual "Back"
        Guid? parentOfCurrentFolderId = null;
        string currentFolderName = "Root";

        if (parentFolderId.HasValue)
        {
            var folderInfo = await _context.Folders
                .Where(f => f.Id == parentFolderId && f.Crate.UserId == userId)
                .Select(f => new { f.Name, f.ParentFolderId })
                .FirstOrDefaultAsync();

            if (folderInfo != null)
            {
                currentFolderName = folderInfo.Name;
                parentOfCurrentFolderId = folderInfo.ParentFolderId;
            }
        }

        return Result<FolderContentsResponse>.Success(new FolderContentsResponse
        {
            Items = pagedItems,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            ParentFolderId = parentFolderId,
            ParentOfCurrentFolderId = parentOfCurrentFolderId,
            FolderName = currentFolderName
        });
    }
}