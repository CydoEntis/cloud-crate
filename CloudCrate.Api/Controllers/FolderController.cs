using System.Security.Claims;
using CloudCrate.Api.Common.Extensions;
using CloudCrate.Api.Models;
using CloudCrate.Application.Common.Interfaces;
using CloudCrate.Application.DTOs.Folder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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


    [HttpGet("root")]
    public async Task<IActionResult> GetRootFolders(Guid crateId)
    {
        var userId = User.GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse<string>.Unauthorized("You do not have permission to access this resource"));

        var result = await _folderService.GetRootFoldersAsync(crateId, userId);

        if (!result.Succeeded)
            return BadRequest(ApiResponse<string>.Error(result.Errors[0].Message));

        return Ok(ApiResponse<object>.Success(result.Data, "Root folders retrieved successfully"));
    }

    [HttpGet("{parentId:guid}/subfolders")]
    public async Task<IActionResult> GetSubfolders(Guid crateId, Guid parentId)
    {
        var userId = User.GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse<string>.Unauthorized("You do not have permission to access this resource"));

        var result = await _folderService.GetSubfoldersAsync(parentId, userId);

        if (!result.Succeeded)
            return BadRequest(ApiResponse<string>.Error(result.Errors[0].Message));

        return Ok(ApiResponse<object>.Success(result.Data, "Subfolders retrieved successfully"));
    }

    [HttpPost]
    public async Task<IActionResult> CreateFolder(Guid crateId, [FromBody] CreateFolderRequest request)
    {
        var userId = User.GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse<string>.Unauthorized("You do not have permission to access this resource"));

        if (crateId != request.CrateId)
            return BadRequest(ApiResponse<string>.Error("Crate ID in route and request body do not match"));

        var result = await _folderService.CreateFolderAsync(request, userId);

        if (!result.Succeeded)
            return BadRequest(ApiResponse<string>.Error(result.Errors[0].Message));

        return Ok(ApiResponse<object>.Success(result.Data, "Folder created successfully"));
    }

    [HttpPut("{folderId:guid}/rename")]
    public async Task<IActionResult> RenameFolder(Guid crateId, Guid folderId, [FromBody] RenameFolderRequest request)
    {
        var userId = User.GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse<string>.Unauthorized("You do not have permission to access this resource"));

        if (folderId != request.FolderId)
            return BadRequest(ApiResponse<string>.Error("Folder ID in route and request body do not match"));

        var result = await _folderService.RenameFolderAsync(folderId, request.NewName, userId);

        if (!result.Succeeded)
            return BadRequest(ApiResponse<string>.Error(result.Errors[0].Message));

        return Ok(ApiResponse<object>.SuccessMessage("Folder renamed successfully"));
    }

    [HttpDelete("{folderId:guid}")]
    public async Task<IActionResult> DeleteFolder(Guid crateId, Guid folderId)
    {
        var userId = User.GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse<string>.Unauthorized("You do not have permission to access this resource"));

        var result = await _folderService.DeleteFolderAsync(folderId, userId);

        if (!result.Succeeded)
            return BadRequest(ApiResponse<string>.Error(result.Errors[0].Message));

        return Ok(ApiResponse<object>.SuccessMessage("Folder deleted successfully"));
    }

    [HttpPut("{folderId:guid}/move")]
    public async Task<IActionResult> MoveFolder(Guid crateId, Guid folderId, [FromBody] MoveFolderRequest request)
    {
        var userId = User.GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse<string>.Unauthorized("You do not have permission to access this resource"));

        var result = await _folderService.MoveFolderAsync(folderId, request.NewParentId, userId);

        if (!result.Succeeded)
            return BadRequest(ApiResponse<string>.Error(result.Errors[0].Message));

        return Ok(ApiResponse<object>.SuccessMessage("Folder moved successfully"));
    }

    [HttpGet("contents/{parentFolderId:guid?}")]
    public async Task<IActionResult> GetFolderContents(Guid crateId, Guid? parentFolderId, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = User.GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse<string>.Unauthorized("You do not have permission to access this resource"));

        var result =
            await _folderService.GetFolderContentsAsync(crateId, parentFolderId, userId, search, page, pageSize);

        if (!result.Succeeded)
            return BadRequest(ApiResponse<string>.Error(result.Errors[0].Message));

        return Ok(ApiResponse<object>.Success(result.Data, "Folder contents retrieved successfully"));
    }
}