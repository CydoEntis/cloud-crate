using System.Security.Claims;
using CloudCrate.Api.Common.Extensions;
using CloudCrate.Api.Models;
using CloudCrate.Application.DTOs;
using CloudCrate.Application.Interfaces.Bulk;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudCrate.Api.Controllers;

[ApiController]
[Route("api/crates/{crateId:guid}/bulk")]
[Authorize]
public class BulkController : ControllerBase
{
    private readonly IBulkService _bulkService;

    public BulkController(IBulkService bulkService)
    {
        _bulkService = bulkService;
    }

    #region Helpers

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

    private ActionResult? ValidateRouteId(Guid routeId, Guid bodyId, string name)
    {
        if (routeId != bodyId)
            return BadRequest(ApiResponse<string>.Error($"{name} ID in route and request body do not match"));
        return null;
    }

    #endregion

    [HttpPost("delete")]
    public async Task<IActionResult> DeleteMultiple(Guid crateId, [FromBody] MultipleDeleteRequest request)
    {
        var validationResult = ValidateUser(out var userId);
        if (validationResult != null) return validationResult;

        validationResult = ValidateRouteId(crateId, crateId, "Crate");
        if (validationResult != null) return validationResult;

        var result = await _bulkService.DeleteAsync(request, userId);
        return result.ToActionResult(this, successStatusCode: 200,
            successMessage: "Selected files and folders deleted successfully");
    }

    [HttpPost("move")]
    public async Task<IActionResult> MoveMultiple(Guid crateId, [FromBody] MultipleMoveRequest request)
    {
        var validationResult = ValidateUser(out var userId);
        if (validationResult != null) return validationResult;

        validationResult = ValidateRouteId(crateId, request.CrateId, "Crate");
        if (validationResult != null) return validationResult;

        var result = await _bulkService.MoveAsync(request, userId);
        return result.ToActionResult(this, successStatusCode: 200,
            successMessage: "Selected files and folders moved successfully");
    }

    [HttpPost("restore")]
    public async Task<IActionResult> RestoreMultiple(Guid crateId, [FromBody] MultipleRestoreRequest request)
    {
        var validationResult = ValidateUser(out var userId);
        if (validationResult != null) return validationResult;

        validationResult = ValidateRouteId(crateId, request.CrateId, "Crate");
        if (validationResult != null) return validationResult;

        var result = await _bulkService.RestoreAsync(request, userId);
        return result.ToActionResult(this, successStatusCode: 200,
            successMessage: "Selected files and folders restored successfully");
    }
}