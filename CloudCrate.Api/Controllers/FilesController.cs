using CloudCrate.Api.DTOs.File.Request;
using CloudCrate.Api.Models;
using CloudCrate.Application.DTOs.File;
using CloudCrate.Application.DTOs.File.Request;
using CloudCrate.Application.Interfaces.File;
using CloudCrate.Api.Common.Extensions;
using CloudCrate.Application.DTOs.File.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudCrate.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/crates/{crateId}/files")]
public class FilesController : BaseController
{
    private readonly IFileService _fileService;

    public FilesController(IFileService fileService)
    {
        _fileService = fileService;
    }

    [HttpPost]
    public async Task<IActionResult> UploadFile(Guid crateId, [FromForm] UploadFileRequest request)
    {
        if (string.IsNullOrWhiteSpace(UserId))
            return Unauthorized(ApiResponse<Guid>.Failure("User is not authenticated", 401));

        if (request.File == null || request.File.Length == 0)
            return BadRequest(ApiResponse<Guid>.Failure("No file uploaded", 400));

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

        var result = await _fileService.UploadFileAsync(uploadRequest, UserId);

        return result.IsSuccess
            ? Created($"/api/crates/{crateId}/files/{result.GetValue()}",
                ApiResponse<Guid>.Success(result.GetValue(), "File uploaded successfully", 201))
            : result.GetError().ToActionResult<Guid>();
    }

    [HttpPost("upload-multiple")]
    public async Task<IActionResult> UploadMultipleFiles(Guid crateId, [FromForm] UploadMultipleFilesRequest request)
    {
        if (string.IsNullOrWhiteSpace(UserId))
            return Unauthorized(ApiResponse<List<Guid>>.Failure("User is not authenticated", 401));

        if (request.Files == null || !request.Files.Any())
            return BadRequest(ApiResponse<List<Guid>>.Failure("No files uploaded", 400));

        var uploadRequests = new List<FileUploadRequest>();
        try
        {
            foreach (var file in request.Files)
            {
                var stream = file.OpenReadStream();
                uploadRequests.Add(new FileUploadRequest
                {
                    CrateId = crateId,
                    FolderId = request.FolderId,
                    FileName = file.FileName,
                    MimeType = file.ContentType,
                    SizeInBytes = file.Length,
                    Content = stream
                });
            }

            var multiUploadRequest = new MultiFileUploadRequest
            {
                Files = uploadRequests
            };

            var result = await _fileService.UploadFilesAsync(multiUploadRequest, UserId);

            return result.IsSuccess
                ? Created("", ApiResponse<List<Guid>>.Success(result.GetValue(), "Files uploaded successfully", 201))
                : result.GetError().ToActionResult<List<Guid>>();
        }
        finally
        {
            foreach (var req in uploadRequests)
                req.Content?.Dispose();
        }
    }

    [HttpGet("{fileId}")]
    public async Task<IActionResult> GetFileMetadata(Guid fileId)
    {
        if (string.IsNullOrWhiteSpace(UserId))
            return Unauthorized(ApiResponse<CrateFileResponse>.Failure("User is not authenticated", 401));

        var result = await _fileService.GetFileAsync(fileId, UserId);

        return result.IsSuccess
            ? Ok(ApiResponse<CrateFileResponse>.Success(result.GetValue(), "File metadata retrieved successfully"))
            : result.GetError().ToActionResult<CrateFileResponse>();
    }

    [HttpGet("{fileId}/download")]
    public async Task<IActionResult> DownloadFile(Guid fileId)
    {
        if (string.IsNullOrWhiteSpace(UserId))
            return Unauthorized();

        // Get file metadata
        var fileResult = await _fileService.GetFileAsync(fileId, UserId);
        if (fileResult.IsFailure)
            return fileResult.GetError().ToActionResult<CrateFileResponse>();

        // Get file content
        var contentResult = await _fileService.DownloadFileAsync(fileId, UserId);
        if (contentResult.IsFailure)
            return contentResult.GetError().ToActionResult<byte[]>();

        var file = fileResult.GetValue();
        var content = contentResult.GetValue();

        return File(content, file.MimeType, file.Name);
    }

    [HttpPost("bulk-download")]
    public async Task<IActionResult> BulkDownloadFiles([FromBody] BulkDownloadRequest request)
    {
        if (string.IsNullOrWhiteSpace(UserId))
            return Unauthorized();

        if (request.FileIds == null || !request.FileIds.Any())
            return BadRequest(ApiResponse<object>.Failure("No files specified", 400));

        var result = await _fileService.DownloadMultipleFilesAsZipAsync(request.FileIds, UserId);

        if (result.IsFailure)
            return result.GetError().ToActionResult<object>();

        var zipContent = result.GetValue();
        var fileName = request.ArchiveName ?? "files.zip";

        return File(zipContent, "application/zip", fileName);
    }

    [HttpDelete("{fileId}/permanent")]
    public async Task<IActionResult> PermanentlyDeleteFile(Guid fileId)
    {
        if (string.IsNullOrWhiteSpace(UserId))
            return Unauthorized(ApiResponse<EmptyResponse>.Failure("User is not authenticated", 401));

        var result = await _fileService.PermanentlyDeleteFilesAsync(new List<Guid> { fileId }, UserId);

        return result.IsSuccess
            ? Ok(ApiResponse<EmptyResponse>.Success(message: "File permanently deleted"))
            : result.GetError().ToActionResult<EmptyResponse>();
    }

    [HttpPut("{fileId}/trash")]
    public async Task<IActionResult> SoftDeleteFile(Guid fileId)
    {
        if (string.IsNullOrWhiteSpace(UserId))
            return Unauthorized(ApiResponse<EmptyResponse>.Failure("User is not authenticated", 401));

        var result = await _fileService.SoftDeleteFileAsync(fileId, UserId);

        return result.IsSuccess
            ? Ok(ApiResponse<EmptyResponse>.Success(message: "File moved to trash"))
            : result.GetError().ToActionResult<EmptyResponse>();
    }

    [HttpPut("{fileId}/restore")]
    public async Task<IActionResult> RestoreFile(Guid fileId)
    {
        if (string.IsNullOrWhiteSpace(UserId))
            return Unauthorized(ApiResponse<EmptyResponse>.Failure("User is not authenticated", 401));

        var result = await _fileService.RestoreFileAsync(fileId, UserId);

        return result.IsSuccess
            ? Ok(ApiResponse<EmptyResponse>.Success(message: "File restored successfully"))
            : result.GetError().ToActionResult<EmptyResponse>();
    }

    [HttpPut("{fileId}/move")]
    public async Task<IActionResult> MoveFile(Guid fileId, [FromBody] MoveFileRequest request)
    {
        if (string.IsNullOrWhiteSpace(UserId))
            return Unauthorized(ApiResponse<EmptyResponse>.Failure("User is not authenticated", 401));

        var result = await _fileService.MoveFileAsync(fileId, request.NewParentId, UserId);

        return result.IsSuccess
            ? Ok(ApiResponse<EmptyResponse>.Success(message: "File moved successfully"))
            : result.GetError().ToActionResult<EmptyResponse>();
    }

    [HttpPost("bulk-delete")]
    public async Task<IActionResult> BulkDeleteFiles([FromBody] BulkOperationRequest request)
    {
        if (string.IsNullOrWhiteSpace(UserId))
            return Unauthorized();

        var result = await _fileService.PermanentlyDeleteFilesAsync(request.FileIds, UserId);

        return result.IsSuccess
            ? Ok(ApiResponse<EmptyResponse>.Success(message: "Files deleted successfully"))
            : result.GetError().ToActionResult<EmptyResponse>();
    }

    [HttpPost("bulk-trash")]
    public async Task<IActionResult> BulkSoftDeleteFiles([FromBody] BulkOperationRequest request)
    {
        if (string.IsNullOrWhiteSpace(UserId))
            return Unauthorized();

        var result = await _fileService.SoftDeleteFilesAsync(request.FileIds, UserId);

        return result.IsSuccess
            ? Ok(ApiResponse<EmptyResponse>.Success(message: "Files moved to trash"))
            : result.GetError().ToActionResult<EmptyResponse>();
    }

    [HttpPut("{fileId:guid}")]
    public async Task<IActionResult> UpdateFile(Guid fileId, [FromBody] UpdateFileRequest request)
    {
        if (string.IsNullOrWhiteSpace(UserId))
            return Unauthorized(ApiResponse<EmptyResponse>.Failure("User is not authenticated", 401));

        if (fileId != request.FileId)
            return BadRequest(
                ApiResponse<EmptyResponse>.Failure("File ID in route and request body do not match", 400));

        var result = await _fileService.UpdateFileAsync(request.FileId, request, UserId);

        if (result.IsSuccess)
            return Ok(ApiResponse<EmptyResponse>.Success(message: "File updated successfully"));

        return result.GetError().ToActionResult<EmptyResponse>();
    }
}