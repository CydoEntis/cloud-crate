using CloudCrate.Application.DTOs;
using CloudCrate.Application.Interfaces.Bulk;
using CloudCrate.Api.Models;
using CloudCrate.Api.Common.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudCrate.Api.Controllers;

[ApiController]
[Route("api/crates/{crateId:guid}/bulk")]
[Authorize]
public class BulkController : BaseController
{
    private readonly IBulkService _bulkService;

    public BulkController(IBulkService bulkService)
    {
        _bulkService = bulkService;
    }

    [HttpPost("delete")]
    public async Task<IActionResult> DeleteMultiple(Guid crateId, [FromBody] MultipleDeleteRequest request)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse<EmptyResponse>.Failure("User is not authenticated", 401));
        }

        if (crateId == Guid.Empty)
        {
            return BadRequest(ApiResponse<EmptyResponse>.Failure("Invalid crate ID", 400));
        }

        var result = await _bulkService.DeleteAsync(request, UserId);

        if (result.IsSuccess)
        {
            return Ok(ApiResponse<EmptyResponse>.Success(
                message: "Selected files and folders deleted successfully"));
        }

        return result.GetError().ToActionResult<EmptyResponse>();
    }

    [HttpPost("move")]
    public async Task<IActionResult> MoveMultiple(Guid crateId, [FromBody] MultipleMoveRequest request)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse<EmptyResponse>.Failure("User is not authenticated", 401));
        }

        if (crateId != request.CrateId)
        {
            return BadRequest(ApiResponse<EmptyResponse>.Failure(
                "Crate ID in route and request body do not match", 400));
        }

        var result = await _bulkService.MoveAsync(request, UserId);

        if (result.IsSuccess)
        {
            return Ok(ApiResponse<EmptyResponse>.Success(
                message: "Selected files and folders moved successfully"));
        }

        return result.GetError().ToActionResult<EmptyResponse>();
    }

    [HttpPost("restore")]
    public async Task<IActionResult> RestoreMultiple(Guid crateId, [FromBody] MultipleRestoreRequest request)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse<EmptyResponse>.Failure("User is not authenticated", 401));
        }

        if (crateId != request.CrateId)
        {
            return BadRequest(ApiResponse<EmptyResponse>.Failure(
                "Crate ID in route and request body do not match", 400));
        }

        var result = await _bulkService.RestoreAsync(request, UserId);

        if (result.IsSuccess)
        {
            return Ok(ApiResponse<EmptyResponse>.Success(
                message: "Selected files and folders restored successfully"));
        }

        return result.GetError().ToActionResult<EmptyResponse>();
    }
}