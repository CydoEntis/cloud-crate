﻿using CloudCrate.Api.Common.Extensions;
using CloudCrate.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
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

    private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpPost]
    public async Task<IActionResult> CreateFolder(Guid crateId, [FromBody] CreateFolderRequest request)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse<string>.Unauthorized("You do not have permission to access this resource"));

        if (crateId != request.CrateId)
            return BadRequest(ApiResponse<string>.Error("Crate ID in route and request body do not match"));

        var result = await _folderService.CreateFolderAsync(request, userId);

        return result.ToActionResult(this, 200, "Folder created successfully");
    }

    [HttpPut("{folderId:guid}/rename")]
    public async Task<IActionResult> RenameFolder(Guid crateId, Guid folderId, [FromBody] RenameFolderRequest request)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse<string>.Unauthorized("You do not have permission to access this resource"));

        if (folderId != request.FolderId)
            return BadRequest(ApiResponse<string>.Error("Folder ID in route and request body do not match"));

        var result = await _folderService.RenameFolderAsync(folderId, request.NewName, userId);

        return result.ToActionResult(this, successStatusCode: 200, successMessage: "Folder renamed successfully");
    }

    [HttpDelete("{folderId:guid}")]
    public async Task<IActionResult> DeleteFolder(Guid crateId, Guid folderId)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse<string>.Unauthorized("You do not have permission to access this resource"));

        var result = await _folderService.DeleteFolderAsync(folderId, userId);

        return result.ToActionResult(this, successStatusCode: 200, successMessage: "Folder deleted successfully");
    }

    [HttpPut("{folderId:guid}/move")]
    public async Task<IActionResult> MoveFolder(Guid crateId, Guid folderId, [FromBody] MoveFolderRequest request)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse<string>.Unauthorized("You do not have permission to access this resource"));

        var result = await _folderService.MoveFolderAsync(folderId, request.NewParentId, userId);

        return result.ToActionResult(this, successStatusCode: 200, successMessage: "Folder moved successfully");
    }

    [HttpGet("contents/{parentFolderId:guid?}")]
    public async Task<IActionResult> GetFolderContents(Guid crateId, Guid? parentFolderId, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse<string>.Unauthorized("You do not have permission to access this resource"));

        var result =
            await _folderService.GetFolderContentsAsync(crateId, parentFolderId, userId, search, page, pageSize);

        return result.ToActionResult(this, successMessage: "Folder contents retrieved successfully");
    }
}