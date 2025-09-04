using CloudCrate.Application.DTOs;
using CloudCrate.Application.Interfaces.Bulk;
using CloudCrate.Api.Models;
using CloudCrate.Application.Common.Models;
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
        var unauthorized = EnsureUserAuthenticated();
        if (unauthorized != null) return unauthorized;

        var routeMismatch = EnsureRouteIdMatches(crateId, crateId, "Crate");
        if (routeMismatch != null) return routeMismatch;

        var result = await _bulkService.DeleteAsync(request, UserId!);
        return Response(ApiResponse.FromResult(result, "Selected files and folders deleted successfully", 200));
    }

    [HttpPost("move")]
    public async Task<IActionResult> MoveMultiple(Guid crateId, [FromBody] MultipleMoveRequest request)
    {
        var unauthorized = EnsureUserAuthenticated();
        if (unauthorized != null) return unauthorized;

        var routeMismatch = EnsureRouteIdMatches(crateId, request.CrateId, "Crate");
        if (routeMismatch != null) return routeMismatch;

        var result = await _bulkService.MoveAsync(request, UserId!);
        return Response(ApiResponse.FromResult(result, "Selected files and folders moved successfully", 200));
    }

    [HttpPost("restore")]
    public async Task<IActionResult> RestoreMultiple(Guid crateId, [FromBody] MultipleRestoreRequest request)
    {
        var unauthorized = EnsureUserAuthenticated();
        if (unauthorized != null) return unauthorized;

        var routeMismatch = EnsureRouteIdMatches(crateId, request.CrateId, "Crate");
        if (routeMismatch != null) return routeMismatch;

        var result = await _bulkService.RestoreAsync(request, UserId!);
        return Response(ApiResponse.FromResult(result, "Selected files and folders restored successfully", 200));
    }
}
