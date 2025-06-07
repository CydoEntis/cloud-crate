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
        if (user == null) return Unauthorized();

        var file = request.File;
        if (file.Length == 0) return BadRequest("No file uploaded");

        await using var stream = file.OpenReadStream();

        var fileData = new FileDto()
        {
            CrateId = crateId,
            FileStream = stream,
            FileName = file.FileName,
            ContentType = file.ContentType,
            Size = file.Length
        };

        var result = await _fileService.UploadFileAsync(user.Id, crateId, fileData);
        return result.Succeeded ? Ok(result.Data) : BadRequest(result.Errors);
    }

    [HttpGet("{fileId}")]
    public async Task<IActionResult> DownloadFile(Guid crateId, Guid fileId)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var result = await _fileService.DownloadFileAsync(user.Id, crateId, fileId);
        if (!result.Succeeded) return BadRequest(result.Errors);

        var file = result.Data;
        return File(file.FileStream, file.ContentType, file.FileName);
    }

    [HttpGet]
    public async Task<IActionResult> GetFiles(Guid crateId)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var result = await _fileService.GetFilesInCrateAsync(user.Id, crateId);
        return result.Succeeded ? Ok(result.Data) : BadRequest(result.Errors);
    }

    [HttpDelete("{fileId}")]
    public async Task<IActionResult> DeleteFile(Guid crateId, Guid fileId)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var result = await _fileService.DeleteFileAsync(user.Id, crateId, fileId);
        return result.Succeeded ? Ok() : BadRequest(result.Errors);
    }
}