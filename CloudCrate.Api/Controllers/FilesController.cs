using CloudCrate.Api.Common.Extensions;
using CloudCrate.Api.Models;
using CloudCrate.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using CloudCrate.Application.Common.Errors;
using System.Security.Claims;
using CloudCrate.Api.DTOs.File.Request;
using CloudCrate.Application.DTOs.File.Request;
using CloudCrate.Application.Interfaces.File;

namespace CloudCrate.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/crates/{crateId}/files")]
public class FilesController : ControllerBase
{
    private readonly IFileService _fileService;
    private readonly UserManager<ApplicationUser> _userManager;

    public FilesController(IFileService fileService, UserManager<ApplicationUser> userManager)
    {
        _fileService = fileService;
        _userManager = userManager;
    }

    private async Task<ApplicationUser?> GetCurrentUserAsync() =>
        await _userManager.GetUserAsync(User);

    [HttpPost]
    public async Task<IActionResult> UploadFile(Guid crateId, [FromForm] UploadFileRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
            return Unauthorized(ApiResponse<string>.Unauthorized("You do not have permission to upload files."));

        if (request.File == null || request.File.Length == 0)
        {
            var error = new Error(Errors.Validation.Failed.Code, "No file uploaded.",
                Errors.Validation.Failed.StatusCode);
            return BadRequest(ApiResponse<string>.ValidationFailed(new List<Error> { error }));
        }

        await using var stream = request.File.OpenReadStream();

        var uploadRequest = new FileUploadRequest
        {
            CrateId = crateId,
            FolderId = request.FolderId,
            FileName = request.File.FileName,
            MimeType = request.File.ContentType,
            SizeInBytes = request.File.Length,
            Content = stream
        };

        var result = await _fileService.UploadFileAsync(uploadRequest, user.Id);

        return result.ToActionResult(this, 200, "File uploaded successfully");
    }

    [HttpGet("{fileId}")]
    public async Task<IActionResult> GetFileMetadata(Guid crateId, Guid fileId)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
            return Unauthorized(ApiResponse<string>.Unauthorized("You do not have permission to view this file."));

        var result = await _fileService.GetFileByIdAsync(fileId, user.Id);
        if (!result.Succeeded)
            return result.ToActionResult(this);

        return result.ToActionResult(this, successMessage: "File metadata retrieved successfully");
    }

    [HttpGet("{fileId}/download")]
    public async Task<IActionResult> DownloadFile(Guid crateId, Guid fileId)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
            return Unauthorized(ApiResponse<string>.Unauthorized("You do not have permission to download files."));

        var fileResult = await _fileService.GetFileByIdAsync(fileId, user.Id);
        if (!fileResult.Succeeded)
            return fileResult.ToActionResult(this);

        var contentResult = await _fileService.DownloadFileAsync(fileId, user.Id);
        if (!contentResult.Succeeded)
            return contentResult.ToActionResult(this);

        var file = fileResult.Value!;

        Response.Headers.Add("Content-Disposition", $"attachment; filename=\"{file.Name}\"");

        return File(contentResult.Value!, file.MimeType, file.Name);
    }

    [HttpGet]
    public async Task<IActionResult> GetFiles(Guid crateId, [FromQuery] Guid? folderId)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
            return Unauthorized(
                ApiResponse<string>.Unauthorized("You do not have permission to access this resource."));

        var result = folderId is null
            ? await _fileService.GetFilesInCrateRootAsync(crateId, user.Id)
            : await _fileService.GetFilesInFolderAsync(crateId, folderId.Value, user.Id);

        return result.ToActionResult(this, successMessage: "Files retrieved successfully");
    }

    [HttpDelete("{fileId}")]
    public async Task<IActionResult> DeleteFile(Guid crateId, Guid fileId)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
            return Unauthorized(ApiResponse<string>.Unauthorized("You do not have permission to delete this file."));

        var result = await _fileService.DeleteFileAsync(fileId, user.Id);

        return result.ToActionResult(this, successStatusCode: 200, successMessage: "File deleted successfully");
    }

    [HttpPut("{fileId:guid}/move")]
    public async Task<IActionResult> MoveFile(Guid crateId, Guid fileId, [FromBody] MoveFileRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(
                ApiResponse<string>.Unauthorized("You do not have permission to access this resource."));

        var result = await _fileService.MoveFileAsync(fileId, request.NewParentId, userId);

        return result.ToActionResult(this, successStatusCode: 200, successMessage: "File moved successfully");
    }
}