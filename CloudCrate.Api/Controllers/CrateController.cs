using CloudCrate.Api.Models;
using CloudCrate.Application.Common.Interfaces;
using CloudCrate.Application.DTOs.Crate;
using CloudCrate.Application.DTOs.File;
using CloudCrate.Application.Common.Models;
using CloudCrate.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CloudCrate.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CrateController : ControllerBase
{
    private readonly ICrateService _crateService;
    private readonly UserManager<ApplicationUser> _userManager;

    public CrateController(ICrateService crateService, UserManager<ApplicationUser> userManager)
    {
        _crateService = crateService;
        _userManager = userManager;
    }

    private async Task<ApplicationUser?> GetCurrentUserAsync() =>
        await _userManager.GetUserAsync(User);

    [HttpPost]
    public async Task<IActionResult> CreateCrate([FromBody] CreateCrateRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var result = await _crateService.CreateCrateAsync(user.Id, request.Name);
        return result.Succeeded ? Ok(result.Data) : BadRequest(result.Errors);
    }

    [HttpGet]
    public async Task<IActionResult> GetUserCrates()
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var result = await _crateService.GetAllCratesAsync(user.Id);
        return result.Succeeded ? Ok(result.Data) : BadRequest(result.Errors);
    }

    [HttpPut("{crateId}/rename")]
    public async Task<IActionResult> RenameCrate(Guid crateId, [FromBody] RenameCrateRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        request.CrateId = crateId;
        var result = await _crateService.RenameCrateAsync(user.Id, request);
        return result.Succeeded ? Ok(result.Data) : BadRequest(result.Errors);
    }

    [HttpPost("{crateId}/files")]
    public async Task<IActionResult> UploadFile(Guid crateId, [FromForm] UploadFileRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var file = request.File;
        if (file.Length == 0) return BadRequest("No file uploaded");

        await using var stream = file.OpenReadStream();

        var fileData = new FileDataRequest
        {
            CrateId = crateId,
            FileStream = stream,
            FileName = file.FileName,
            ContentType = file.ContentType,
            Size = file.Length
        };

        var result = await _crateService.UploadFileAsync(user.Id, fileData);
        return result.Succeeded ? Ok(result.Data) : BadRequest(result.Errors);
    }

    [HttpGet("{crateId}/files/{fileId}")]
    public async Task<IActionResult> DownloadFile(Guid crateId, Guid fileId)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var result =
            await _crateService.DownloadFileAsync(user.Id,
                new DownloadFileRequest { CrateId = crateId, FileId = fileId });
        if (!result.Succeeded) return BadRequest(result.Errors);

        var file = result.Data;
        return File(file.FileStream, file.ContentType, file.FileName);
    }

    [HttpGet("{crateId}/files")]
    public async Task<IActionResult> GetFiles(Guid crateId)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var result = await _crateService.GetFilesInCrateAsync(crateId, user.Id);
        return result.Succeeded ? Ok(result.Data) : BadRequest(result.Errors);
    }

    [HttpDelete("{crateId}/files/{fileId}")]
    public async Task<IActionResult> DeleteFile(Guid crateId, Guid fileId)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var result = await _crateService.DeleteFileAsync(crateId, user.Id, fileId);
        return result.Succeeded ? Ok() : BadRequest(result.Errors);
    }
}