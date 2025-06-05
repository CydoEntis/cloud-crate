using CloudCrate.Api.Models;
using CloudCrate.Application.Common.Interfaces;
using CloudCrate.Application.DTOs.Crate;
using CloudCrate.Application.DTOs.File;
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

        var crate = await _crateService.CreateCrateAsync(user.Id, request.Name);
        return Ok(crate);
    }

    [HttpGet]
    public async Task<IActionResult> GetUserCrates()
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var crates = await _crateService.GetAllCratesAsync(user.Id);
        return Ok(crates);
    }

    [HttpPut("{crateId}/rename")]
    public async Task<IActionResult> RenameCrate(Guid crateId, [FromBody] RenameCrateRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var updatedCrate = await _crateService.RenameCrateAsync(crateId, user.Id, request.NewName);
        return Ok(updatedCrate);
    }


    [HttpPost("{crateId}/files")]
    public async Task<IActionResult> UploadFile(Guid crateId, [FromForm] UploadFileRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var file = request.File;
        if (file.Length == 0) return BadRequest("No file uploaded");

        await using var stream = file.OpenReadStream();

        var uploadDto = new UploadFileDto
        {
            FileStream = stream,
            FileName = file.FileName,
            ContentType = file.ContentType,
            Size = file.Length
        };

        await _crateService.UploadFileAsync(crateId, user.Id, uploadDto);

        return Ok();
    }

    [HttpGet("{crateId}/files/{fileId}")]
    public async Task<IActionResult> DownloadFile(Guid crateId, Guid fileId)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var (stream, fileName) = await _crateService.DownloadFileAsync(crateId, user.Id, fileId);
        return File(stream, "application/octet-stream", fileName);
    }
}