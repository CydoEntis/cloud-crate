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
            return Unauthorized();

        if (request.File == null || request.File.Length == 0)
            return BadRequest("No file uploaded");

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
        return result.Succeeded ? Ok(result.Data) : BadRequest(result.Errors);
    }

    [HttpGet("{fileId}")]
    public async Task<IActionResult> DownloadFile(Guid crateId, Guid fileId)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
            return Unauthorized();

        var fileResult = await _fileService.GetFileByIdAsync(fileId, user.Id);
        if (!fileResult.Succeeded)
            return BadRequest(fileResult.Errors);

        var file = fileResult.Data;

        // Normally this is where you’d fetch the binary blob from storage.
        var contentResult = await _fileService.DownloadFileAsync(fileId, user.Id);
        if (!contentResult.Succeeded)
            return BadRequest(contentResult.Errors);

        return File(contentResult.Data, file.MimeType, file.Name);
    }

    [HttpGet]
    public async Task<IActionResult> GetFiles(Guid crateId)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
            return Unauthorized();

        var result = await _fileService.GetFilesInCrateRootAsync(crateId, user.Id);
        return result.Succeeded ? Ok(result.Data) : BadRequest(result.Errors);
    }

    [HttpDelete("{fileId}")]
    public async Task<IActionResult> DeleteFile(Guid crateId, Guid fileId)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
            return Unauthorized();

        var result = await _fileService.DeleteFileAsync(fileId, user.Id);
        return result.Succeeded ? Ok() : BadRequest(result.Errors);
    }
}