using CloudCrate.Api.Common.Extensions;
using CloudCrate.Api.Models;
using CloudCrate.Api.Requests.File;
using CloudCrate.Application.Common.Interfaces;
using CloudCrate.Application.DTOs.File;
using CloudCrate.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

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
            return BadRequest(ApiResponse<string>.Error("No file uploaded"));

        await using var stream = request.File.OpenReadStream();

        var uploadRequest = new Application.DTOs.File.FileUploadRequest
        {
            CrateId = crateId,
            FolderId = request.FolderId,
            FileName = request.File.FileName,
            MimeType = request.File.ContentType,
            SizeInBytes = request.File.Length,
            Content = stream
        };

        var result = await _fileService.UploadFileAsync(uploadRequest, user.Id);

        return result.Succeeded
            ? Ok(ApiResponse<object>.Success(result.Data, "File uploaded successfully"))
            : BadRequest(ApiResponse<string>.ValidationFailed(result.Errors));
    }

    [HttpGet("{fileId}")]
    public async Task<IActionResult> DownloadFile(Guid crateId, Guid fileId)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
            return Unauthorized(ApiResponse<string>.Unauthorized("You do not have permission to download files."));

        var fileResult = await _fileService.GetFileByIdAsync(fileId, user.Id);
        if (!fileResult.Succeeded)
            return NotFound(ApiResponse<string>.Error(fileResult.Errors[0].Message, 404));

        var contentResult = await _fileService.DownloadFileAsync(fileId, user.Id);
        if (!contentResult.Succeeded)
            return BadRequest(ApiResponse<string>.Error(contentResult.Errors[0].Message));

        var file = fileResult.Data!;
        return File(contentResult.Data!, file.MimeType, file.Name);
    }

    [HttpGet]
    public async Task<IActionResult> GetFiles(Guid crateId, [FromQuery] Guid? folderId)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
            return Unauthorized(ApiResponse<string>.Unauthorized("You do not have permission to access this resource"));

        var result = folderId is null
            ? await _fileService.GetFilesInCrateRootAsync(crateId, user.Id)
            : await _fileService.GetFilesInFolderAsync(crateId, folderId.Value, user.Id);

        return result.Succeeded
            ? Ok(ApiResponse<object>.Success(result.Data, "Files retrieved successfully"))
            : BadRequest(ApiResponse<string>.ValidationFailed(result.Errors));
    }

    [HttpDelete("{fileId}")]
    public async Task<IActionResult> DeleteFile(Guid crateId, Guid fileId)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
            return Unauthorized(ApiResponse<string>.Unauthorized("You do not have permission to delete this file."));

        var result = await _fileService.DeleteFileAsync(fileId, user.Id);

        return result.Succeeded
            ? Ok(ApiResponse<object>.SuccessMessage("File deleted successfully"))
            : BadRequest(ApiResponse<string>.ValidationFailed(result.Errors));
    }

    [HttpPut("{fileId:guid}/move")]
    public async Task<IActionResult> MoveFile(Guid crateId, Guid fileId, [FromBody] MoveFileRequest request)
    {
        var userId = User.GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse<string>.Unauthorized("You do not have permission to access this resource"));

        var result = await _fileService.MoveFileAsync(fileId, request.NewParentId, userId);

        if (!result.Succeeded)
            return BadRequest(ApiResponse<string>.Error(result.Errors[0].Message));

        return Ok(ApiResponse<object>.SuccessMessage("File moved successfully"));
    }
}