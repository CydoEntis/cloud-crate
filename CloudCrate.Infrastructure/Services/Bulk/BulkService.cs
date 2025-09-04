using System.Transactions;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs;
using CloudCrate.Application.Interfaces.Bulk;
using CloudCrate.Application.Interfaces.File;
using CloudCrate.Application.Interfaces.Folder;

namespace CloudCrate.Infrastructure.Services.Bulk
{
    public class BulkService : IBulkService
    {
        private readonly IFileService _fileService;
        private readonly IFolderService _folderService;

        public BulkService(IFileService fileService, IFolderService folderService)
        {
            _fileService = fileService;
            _folderService = folderService;
        }

        public async Task<Result> MoveAsync(MultipleMoveRequest request, string userId)
        {
            if (request.NewParentId.HasValue && request.NewParentId.Value == Guid.Empty)
                request.NewParentId = null;

            using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

            foreach (var folderId in request.FolderIds)
            {
                var folderResult = await _folderService.MoveFolderAsync(folderId, request.NewParentId, userId);
                if (folderResult.IsFailure)
                    return folderResult;
            }

            var fileResult = await _fileService.MoveFilesAsync(request.FileIds, request.NewParentId, userId);
            if (fileResult.IsFailure)
                return fileResult;

            scope.Complete();
            return Result.Success();
        }

        public async Task<Result> DeleteAsync(MultipleDeleteRequest request, string userId)
        {
            using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

            var result = await _folderService.DeleteMultipleAsync(request, userId);
            if (result.IsFailure)
                return result;

            scope.Complete();
            return Result.Success();
        }

        public async Task<Result> RestoreAsync(MultipleRestoreRequest request, string userId)
        {
            using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

            foreach (var folderId in request.FolderIds)
            {
                var folderResult = await _folderService.RestoreFolderAsync(folderId, userId);
                if (folderResult.IsFailure)
                    return folderResult;
            }

            var fileResult = await _fileService.RestoreFilesAsync(request.FileIds, userId);
            if (fileResult.IsFailure)
                return fileResult;

            scope.Complete();
            return Result.Success();
        }
    }
}