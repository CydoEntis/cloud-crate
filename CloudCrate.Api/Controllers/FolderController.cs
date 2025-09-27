using CloudCrate.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CloudCrate.Application.DTOs.Folder;
using CloudCrate.Application.DTOs.Folder.Request;
using CloudCrate.Application.DTOs.Folder.Response;
using CloudCrate.Application.Interfaces.Folder;
using CloudCrate.Api.Common.Extensions;
using CloudCrate.Application.DTOs.Pagination;

namespace CloudCrate.Api.Controllers;

[ApiController]
[Route("api/crates/{crateId:guid}/folders")]
[Authorize]
public class FolderController : BaseController
{
    private readonly IFolderService _folderService;

    public FolderController(IFolderService folderService)
    {
        _folderService = folderService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateFolder(Guid crateId, [FromBody] CreateFolderRequest request)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse<Guid>.Failure("User is not authenticated", 401));
        }

        if (crateId != request.CrateId)
        {
            return BadRequest(ApiResponse<Guid>.Failure("Crate ID in route and request body do not match", 400));
        }

        var result = await _folderService.CreateFolderAsync(request, UserId);

        if (result.IsSuccess)
        {
            return Created("", ApiResponse<Guid>.Success(
                data: result.GetValue(),
                message: "Folder created successfully",
                statusCode: 201));
        }

        return result.GetError().ToActionResult<Guid>();
    }

    [HttpPut("{folderId:guid}")]
    public async Task<IActionResult> UpdateFolder(Guid folderId, [FromBody] UpdateFolderRequest request)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse<EmptyResponse>.Failure("User is not authenticated", 401));
        }

        if (folderId != request.FolderId)
        {
            return BadRequest(
                ApiResponse<EmptyResponse>.Failure("Folder ID in route and request body do not match", 400));
        }

        var result = await _folderService.UpdateFolderAsync(request.FolderId, request, UserId);

        if (result.IsSuccess)
        {
            return Ok(ApiResponse<EmptyResponse>.Success(message: "Folder updated successfully"));
        }

        return result.GetError().ToActionResult<EmptyResponse>();
    }

    [HttpDelete("{folderId:guid}")]
    public async Task<IActionResult> DeleteFolder(Guid folderId)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse<EmptyResponse>.Failure("User is not authenticated", 401));
        }

        var result = await _folderService.DeleteFolderAsync(folderId, UserId);

        if (result.IsSuccess)
        {
            return Ok(ApiResponse<EmptyResponse>.Success(message: "Folder deleted successfully (soft delete)"));
        }

        return result.GetError().ToActionResult<EmptyResponse>();
    }

    [HttpDelete("{folderId:guid}/permanent")]
    public async Task<IActionResult> PermanentlyDeleteFolder(Guid folderId)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse<EmptyResponse>.Failure("User is not authenticated", 401));
        }

        var result = await _folderService.PermanentlyDeleteFolderAsync(folderId, UserId);

        if (result.IsSuccess)
        {
            return Ok(ApiResponse<EmptyResponse>.Success(message: "Folder permanently deleted successfully"));
        }

        return result.GetError().ToActionResult<EmptyResponse>();
    }

    [HttpPut("{folderId:guid}/move")]
    public async Task<IActionResult> MoveFolder(Guid folderId, [FromBody] MoveFolderRequest request)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse<EmptyResponse>.Failure("User is not authenticated", 401));
        }

        var result = await _folderService.MoveFolderAsync(folderId, request.NewParentId, UserId);

        if (result.IsSuccess)
        {
            return Ok(ApiResponse<EmptyResponse>.Success(message: "Folder moved successfully"));
        }

        return result.GetError().ToActionResult<EmptyResponse>();
    }

    [HttpGet("contents/{parentFolderId:guid?}")]
    public async Task<IActionResult> GetFolderContents(Guid crateId, Guid? parentFolderId,
        [FromQuery] FolderContentsParameters queryParameters)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse<FolderContentsResponse>.Failure("User is not authenticated", 401));
        }

        queryParameters.CrateId = crateId;
        queryParameters.FolderId = parentFolderId;
        queryParameters.UserId = UserId;

        var result = await _folderService.GetFolderContentsAsync(queryParameters);

        if (result.IsSuccess)
        {
            return Ok(ApiResponse<FolderContentsResponse>.Success(
                data: result.GetValue(),
                message: "Folder contents retrieved successfully"));
        }

        return result.GetError().ToActionResult<FolderContentsResponse>();
    }

    [HttpGet("available-move-targets")]
    public async Task<IActionResult> GetAvailableMoveTargets(
        Guid crateId,
        [FromQuery] GetAvailableMoveTargetsRequest request)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse<PaginatedResult<FolderResponse>>.Failure("User is not authenticated", 401));
        }

        // Ensure crateId consistency
        request.CrateId = crateId;

        var result = await _folderService.GetAvailableMoveFoldersAsync(request, UserId);

        if (result.IsSuccess)
        {
            return Ok(ApiResponse<PaginatedResult<FolderResponse>>.Success(
                data: result.GetValue(),
                message: "Available move targets retrieved successfully"));
        }

        return result.GetError().ToActionResult<PaginatedResult<FolderResponse>>();
    }

    [HttpGet("{folderId:guid}/download")]
    public async Task<IActionResult> DownloadFolder(Guid folderId)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse<FolderDownloadResult>.Failure("User is not authenticated", 401));
        }

        var result = await _folderService.DownloadFolderAsync(folderId, UserId);

        if (result.IsSuccess)
        {
            var downloadResult = result.GetValue();

            return File(
                downloadResult.FileBytes,
                "application/zip",
                $"{downloadResult.FileName}.zip"
            );
        }

        return result.GetError().ToActionResult<FolderDownloadResult>();
    }

    [HttpPut("{folderId:guid}/restore")]
    public async Task<IActionResult> RestoreFolder(Guid folderId)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse<EmptyResponse>.Failure("User is not authenticated", 401));
        }

        var result = await _folderService.RestoreFolderAsync(folderId, UserId);

        if (result.IsSuccess)
        {
            return Ok(ApiResponse<EmptyResponse>.Success(message: "Folder restored successfully"));
        }

        return result.GetError().ToActionResult<EmptyResponse>();
    }

    [HttpDelete("trash")]
    public async Task<IActionResult> EmptyTrash(Guid crateId)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse<EmptyResponse>.Failure("User is not authenticated", 401));
        }

        var result = await _folderService.EmptyTrashAsync(crateId, UserId);

        if (result.IsSuccess)
        {
            return Ok(ApiResponse<EmptyResponse>.Success(message: "Trash emptied successfully"));
        }

        return result.GetError().ToActionResult<EmptyResponse>();
    }
}