using CloudCrate.Api.Common.Extensions;
using CloudCrate.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CloudCrate.Application.DTOs.Folder;
using CloudCrate.Application.DTOs.Folder.Request;
using CloudCrate.Application.Interfaces.Folder;

namespace CloudCrate.Api.Controllers;

[ApiController]
[Route("api/crates/{crateId:guid}/folders")]
[Authorize]
public class FolderController : ControllerBase
{
    private readonly IFolderService _folderService;

    public FolderController(IFolderService folderService)
    {
        _folderService = folderService;
    }

    #region Helpers

    private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private ActionResult? ValidateUser(out string userId)
    {
        userId = GetUserId() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = null!;
            return Unauthorized(ApiResponse<string>.Unauthorized("You do not have permission to access this resource"));
        }

        return null;
    }

    private ActionResult? ValidateRouteId(Guid routeId, Guid bodyId, string name)
    {
        if (routeId != bodyId)
            return BadRequest(ApiResponse<string>.Error($"{name} ID in route and request body do not match"));
        return null;
    }

    #endregion

    [HttpPost]
    public async Task<IActionResult> CreateFolder(Guid crateId, [FromBody] CreateFolderRequest request)
    {
        var validationResult = ValidateUser(out var userId);
        if (validationResult != null) return validationResult;

        validationResult = ValidateRouteId(crateId, request.CrateId, "Crate");
        if (validationResult != null) return validationResult;

        var result = await _folderService.CreateFolderAsync(request, userId);
        return result.ToActionResult(this, successStatusCode: 200, successMessage: "Folder created successfully");
    }

    [HttpPut("{folderId:guid}")]
    public async Task<IActionResult> UpdateFolder(Guid crateId, Guid folderId, [FromBody] UpdateFolderRequest request)
    {
        var validationResult = ValidateUser(out var userId);
        if (validationResult != null) return validationResult;

        validationResult = ValidateRouteId(folderId, request.FolderId, "Folder");
        if (validationResult != null) return validationResult;

        var result = await _folderService.UpdateFolderAsync(request.FolderId, request, userId);
        return result.ToActionResult(this, successStatusCode: 200, successMessage: "Folder updated successfully");
    }

    [HttpDelete("{folderId:guid}")]
    public async Task<IActionResult> DeleteFolder(Guid crateId, Guid folderId)
    {
        var validationResult = ValidateUser(out var userId);
        if (validationResult != null) return validationResult;

        var result = await _folderService.DeleteFolderAsync(folderId, userId);
        return result.ToActionResult(this, successStatusCode: 200,
            successMessage: "Folder deleted successfully (soft delete)");
    }

    [HttpDelete("{folderId:guid}/permanent")]
    public async Task<IActionResult> PermanentlyDeleteFolder(Guid folderId)
    {
        var validationResult = ValidateUser(out var userId);
        if (validationResult != null) return validationResult;

        var result = await _folderService.PermanentlyDeleteFolderAsync(folderId);
        return result.ToActionResult(this, successStatusCode: 200,
            successMessage: "Folder permanently deleted successfully");
    }

    [HttpPut("{folderId:guid}/move")]
    public async Task<IActionResult> MoveFolder(Guid crateId, Guid folderId, [FromBody] MoveFolderRequest request)
    {
        var validationResult = ValidateUser(out var userId);
        if (validationResult != null) return validationResult;

        var result = await _folderService.MoveFolderAsync(folderId, request.NewParentId, userId);
        return result.ToActionResult(this, successStatusCode: 200, successMessage: "Folder moved successfully");
    }

    [HttpGet("contents/{parentFolderId:guid?}")]
    public async Task<IActionResult> GetFolderContents(
        Guid crateId,
        Guid? parentFolderId,
        [FromQuery] FolderQueryParameters queryParameters)
    {
        var validationResult = ValidateUser(out var userId);
        if (validationResult != null) return validationResult;

        queryParameters.CrateId = crateId;
        queryParameters.ParentFolderId = parentFolderId;
        queryParameters.UserId = userId;

        var result = await _folderService.GetFolderContentsAsync(queryParameters);
        return result.ToActionResult(this, successMessage: "Folder contents retrieved successfully");
    }

    [HttpGet("folders/{folderId}/download")]
    public async Task<IActionResult> DownloadFolder(Guid folderId)
    {
        var validationResult = ValidateUser(out var userId);
        if (validationResult != null) return validationResult;

        var result = await _folderService.DownloadFolderAsync(folderId, userId);
        return result.ToActionResult(this, successMessage: "Folder downloaded successfully");
    }
}