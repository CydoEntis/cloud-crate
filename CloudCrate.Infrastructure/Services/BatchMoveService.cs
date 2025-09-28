using CloudCrate.Application.Errors;
using CloudCrate.Application.Interfaces;
using CloudCrate.Application.Interfaces.File;
using CloudCrate.Application.Interfaces.Folder;
using CloudCrate.Application.Models;
using Microsoft.Extensions.Logging;

namespace CloudCrate.Infrastructure.Services;

public class BatchMoveService : IBatchMoveService
{
    private readonly IFileService _fileService;
    private readonly IFolderService _folderService;
    private readonly ILogger<BatchMoveService> _logger;

    public BatchMoveService(
        IFileService fileService,
        IFolderService folderService,
        ILogger<BatchMoveService> logger)
    {
        _fileService = fileService;
        _folderService = folderService;
        _logger = logger;
    }

    public async Task<Result> MoveItemsAsync(List<Guid> fileIds, List<Guid> folderIds, Guid? newParentId, string userId)
    {
        try
        {
            // Move files first
            if (fileIds.Any())
            {
                foreach (var fileId in fileIds)
                {
                    var result = await _fileService.MoveFileAsync(fileId, newParentId, userId);
                    if (result.IsFailure)
                        return result;
                }
            }

            // Then move folders
            if (folderIds.Any())
            {
                foreach (var folderId in folderIds)
                {
                    var result = await _folderService.MoveFolderAsync(folderId, newParentId, userId);
                    if (result.IsFailure)
                        return result;
                }
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in bulk move for user {UserId}", userId);
            return Result.Failure(new InternalError(ex.Message));
        }
    }
}