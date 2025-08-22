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

    #region Helpers

    private async Task<ApplicationUser?> GetCurrentUserAsync() =>
        await _userManager.GetUserAsync(User);

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

    #endregion

    [HttpPost]
    public async Task<IActionResult> UploadFile(Guid crateId, [FromForm] UploadFileRequest request)
    {
        var validationResult = ValidateUser(out var userId);
        if (validationResult != null) return validationResult;

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

        var result = await _fileService.UploadFileAsync(uploadRequest, userId);

        return result.ToActionResult(this, 200, "File uploaded successfully");
    }

    [HttpGet("{fileId}")]
    public async Task<IActionResult> GetFileMetadata(Guid crateId, Guid fileId)
    {
        var validationResult = ValidateUser(out var userId);
        if (validationResult != null) return validationResult;

        var result = await _fileService.GetFileByIdAsync(fileId, userId);
        return result.ToActionResult(this, successMessage: "File metadata retrieved successfully");
    }

    [HttpGet("{fileId}/download")]
    public async Task<IActionResult> DownloadFile(Guid crateId, Guid fileId)
    {
        var validationResult = ValidateUser(out var userId);
        if (validationResult != null) return validationResult;

        var fileResult = await _fileService.GetFileByIdAsync(fileId, userId);
        if (!fileResult.Succeeded) return fileResult.ToActionResult(this);

        var contentResult = await _fileService.DownloadFileAsync(fileId, userId);
        if (!contentResult.Succeeded) return contentResult.ToActionResult(this);

        var file = fileResult.Value!;

        Response.Headers.Add("Content-Disposition", $"attachment; filename=\"{file.Name}\"");

        return File(contentResult.Value!, file.MimeType, file.Name);
    }

    [HttpDelete("{fileId}")]
    public async Task<IActionResult> DeleteFile(Guid crateId, Guid fileId)
    {
        var validationResult = ValidateUser(out var userId);
        if (validationResult != null) return validationResult;

        var result = await _fileService.DeleteFileAsync(fileId, userId);
        return result.ToActionResult(this, successStatusCode: 200, successMessage: "File deleted successfully");
    }

    [HttpPut("{fileId:guid}/move")]
    public async Task<IActionResult> MoveFile(Guid crateId, Guid fileId, [FromBody] MoveFileRequest request)
    {
        var validationResult = ValidateUser(out var userId);
        if (validationResult != null) return validationResult;

        var result = await _fileService.MoveFileAsync(fileId, request.NewParentId, userId);
        return result.ToActionResult(this, successStatusCode: 200, successMessage: "File moved successfully");
    }
}