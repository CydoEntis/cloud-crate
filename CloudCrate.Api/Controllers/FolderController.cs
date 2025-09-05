using CloudCrate.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CloudCrate.Application.DTOs.Folder;
using CloudCrate.Application.DTOs.Folder.Request;
using CloudCrate.Application.DTOs.Folder.Response;
using CloudCrate.Application.Interfaces.Folder;

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
        var unauthorized = EnsureUserAuthenticated();
        if (unauthorized != null) return unauthorized;

        var routeMismatch = EnsureRouteIdMatches(crateId, request.CrateId, "Crate");
        if (routeMismatch != null) return routeMismatch;

        var result = await _folderService.CreateFolderAsync(request, UserId!);
        return Response(ApiResponse<Guid>.FromResult(result, "Folder created successfully", 201));
    }

    [HttpPut("{folderId:guid}")]
    public async Task<IActionResult> UpdateFolder(Guid crateId, Guid folderId, [FromBody] UpdateFolderRequest request)
    {
        var unauthorized = EnsureUserAuthenticated();
        if (unauthorized != null) return unauthorized;

        var routeMismatch = EnsureRouteIdMatches(folderId, request.FolderId, "Folder");
        if (routeMismatch != null) return routeMismatch;

        var result = await _folderService.UpdateFolderAsync(request.FolderId, request, UserId!);
        return Response(ApiResponse.FromResult(result, "Folder updated successfully"));
    }

    [HttpDelete("{folderId:guid}")]
    public async Task<IActionResult> DeleteFolder(Guid crateId, Guid folderId)
    {
        var unauthorized = EnsureUserAuthenticated();
        if (unauthorized != null) return unauthorized;

        var result = await _folderService.DeleteFolderAsync(folderId, UserId!);
        return Response(ApiResponse.FromResult(result, "Folder deleted successfully (soft delete)"));
    }

    [HttpDelete("{folderId:guid}/permanent")]
    public async Task<IActionResult> PermanentlyDeleteFolder(Guid folderId)
    {
        var unauthorized = EnsureUserAuthenticated();
        if (unauthorized != null) return unauthorized;

        var result = await _folderService.PermanentlyDeleteFolderAsync(folderId, UserId!);
        return Response(ApiResponse.FromResult(result, "Folder permanently deleted successfully"));
    }

    [HttpPut("{folderId:guid}/move")]
    public async Task<IActionResult> MoveFolder(Guid crateId, Guid folderId, [FromBody] MoveFolderRequest request)
    {
        var unauthorized = EnsureUserAuthenticated();
        if (unauthorized != null) return unauthorized;

        var result = await _folderService.MoveFolderAsync(folderId, request.NewParentId, UserId!);
        return Response(ApiResponse.FromResult(result, "Folder moved successfully"));
    }

    [HttpGet("contents/{parentFolderId:guid?}")]
    public async Task<IActionResult> GetFolderContents(Guid crateId, Guid? parentFolderId,
        [FromQuery] FolderContentsParameters queryParameters)
    {
        var unauthorized = EnsureUserAuthenticated();
        if (unauthorized != null) return unauthorized;

        queryParameters.CrateId = crateId;
        queryParameters.FolderId = parentFolderId;
        queryParameters.UserId = UserId!;

        var result = await _folderService.GetFolderContentsAsync(queryParameters);
        return Response(
            ApiResponse<FolderContentsResponse>.FromResult(result, "Folder contents retrieved successfully"));
    }

    [HttpGet("available-move-targets")]
    public async Task<IActionResult> GetAvailableMoveTargets(Guid crateId, Guid? excludeFolderId = null)
    {
        var unauthorized = EnsureUserAuthenticated();
        if (unauthorized != null) return unauthorized;

        var result = await _folderService.GetAvailableMoveFoldersAsync(crateId, excludeFolderId);
        return Response(
            ApiResponse<List<FolderResponse>>.FromResult(result, "Available move targets retrieved successfully"));
    }

    [HttpGet("folders/{folderId}/download")]
    public async Task<IActionResult> DownloadFolder(Guid folderId)
    {
        var unauthorized = EnsureUserAuthenticated();
        if (unauthorized != null) return unauthorized;

        var result = await _folderService.DownloadFolderAsync(folderId, UserId!);
        return Response(ApiResponse<FolderDownloadResult>.FromResult(result, "Folder downloaded successfully"));
    }

    [HttpPut("{folderId:guid}/restore")]
    public async Task<IActionResult> RestoreFolder(Guid crateId, Guid folderId)
    {
        var unauthorized = EnsureUserAuthenticated();
        if (unauthorized != null) return unauthorized;

        var result = await _folderService.RestoreFolderAsync(folderId, UserId!);
        return Response(ApiResponse.FromResult(result, "Folder restored successfully"));
    }
}