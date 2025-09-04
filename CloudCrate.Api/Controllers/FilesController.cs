using CloudCrate.Api.DTOs.File.Request;
using CloudCrate.Api.Models;
using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.File;
using CloudCrate.Application.DTOs.File.Request;
using CloudCrate.Application.Interfaces.File;
using CloudCrate.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CloudCrate.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/crates/{crateId}/files")]
public class FilesController : BaseController
{
    private readonly IFileService _fileService;
    private readonly UserManager<ApplicationUser> _userManager;

    public FilesController(IFileService fileService, UserManager<ApplicationUser> userManager)
    {
        _fileService = fileService;
        _userManager = userManager;
    }

    [HttpPost]
    public async Task<IActionResult> UploadFile(Guid crateId, [FromForm] UploadFileRequest request)
    {
        var unauthorized = EnsureUserAuthenticated();
        if (unauthorized != null) return unauthorized;

        if (request.File == null || request.File.Length == 0)
        {
            var failureResult = Result<Guid>.Failure(
                Error.Validation("No file uploaded.", "File")
            );

            var errorResponse = ApiResponse<Guid>.FromResult(failureResult);

            return Response(errorResponse);
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

        var result = await _fileService.UploadFileAsync(uploadRequest, UserId!);

        return Response(ApiResponse<Guid>.FromResult(result, "File uploaded successfully", 201));
    }


    [HttpGet("{fileId}")]
    public async Task<IActionResult> GetFileMetadata(Guid crateId, Guid fileId)
    {
        var unauthorized = EnsureUserAuthenticated();
        if (unauthorized != null) return unauthorized;

        var result = await _fileService.FetchFileResponseAsync(fileId, UserId!);
        return Response(
            ApiResponse<CrateFileResponse>.FromResult(result, "File metadata retrieved successfully"));
    }

    [HttpGet("{fileId}/download")]
    public async Task<IActionResult> DownloadFile(Guid crateId, Guid fileId)
    {
        var unauthorized = EnsureUserAuthenticated();
        if (unauthorized != null) return unauthorized;

        var fileResult = await _fileService.FetchFileResponseAsync(fileId, UserId!);
        if (fileResult.IsFailure)
            return Response(
                ApiResponse<CrateFileResponse>.FromResult(fileResult));

        var contentResult = await _fileService.DownloadFileAsync(fileId, UserId!);
        if (contentResult.IsFailure)
            return Response(ApiResponse<byte[]>.FromResult(contentResult));

        var file = fileResult.Value!;
        HttpContext.Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{file.Name}\"");

        return File(contentResult.Value!, file.MimeType, file.Name);
    }


    [HttpDelete("{fileId}")]
    public async Task<IActionResult> DeleteFile(Guid crateId, Guid fileId)
    {
        var unauthorized = EnsureUserAuthenticated();
        if (unauthorized != null) return unauthorized;

        var result = await _fileService.DeleteFileAsync(fileId, UserId!);
        return Response(ApiResponse.FromResult(result, "File deleted successfully", 204));
    }

    [HttpPut("{fileId:guid}/move")]
    public async Task<IActionResult> MoveFile(Guid crateId, Guid fileId, [FromBody] MoveFileRequest request)
    {
        var unauthorized = EnsureUserAuthenticated();
        if (unauthorized != null) return unauthorized;

        var result = await _fileService.MoveFileAsync(fileId, request.NewParentId, UserId!);
        return Response(ApiResponse.FromResult(result, "File moved successfully", 200));
    }
}