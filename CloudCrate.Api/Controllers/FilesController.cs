using CloudCrate.Api.Models;
using CloudCrate.Application.Common.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace CloudCrate.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FilesController : ControllerBase
{
    private readonly IFileStorageService _fileStorageService;
    private static readonly string[] PermittedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff" };
    private readonly IValidator<UploadFileRequest> _validator;


    public FilesController(IFileStorageService fileStorageService, IValidator<UploadFileRequest> validator)
    {
        _fileStorageService = fileStorageService;
        _validator = validator;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload([FromForm] UploadFileRequest request)
    {
        var validationResult = await _validator.ValidateAsync(request);

        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.Errors.Select(e => new
            {
                Property = e.PropertyName,
                Error = e.ErrorMessage
            }));
        }

        await using var stream = request.File.OpenReadStream();

        var savedFileName = await _fileStorageService.UploadAsync(stream, request.File.FileName);

        return Ok(new { FileName = savedFileName });
    }


    [HttpGet("download/{fileName}")]
    public async Task<IActionResult> DownloadAsync(string fileName)
    {
        try
        {
            var fileStream = await _fileStorageService.DownloadAsync(fileName);
            return File(fileStream, "application/octet-stream", fileName);
        }
        catch (FileNotFoundException)
        {
            return NotFound($"File '{fileName}' not found");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error downloading file: {ex.Message}");
        }
    }
}