using System.Security.Claims;
using CloudCrate.Api.Models;
using CloudCrate.Api.Requests.Crate;
using CloudCrate.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudCrate.Api.Controllers;

[ApiController]
[Route("api/crates")]
[Authorize]
public class CrateController : ControllerBase
{
    private readonly ICrateService _crateService;

    public CrateController(ICrateService crateService)
    {
        _crateService = crateService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateCrate([FromBody] CreateCrateRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(ApiResponse<string>.Unauthorized("You do not have permission to access this resource"));

        var result = await _crateService.CreateCrateAsync(userId, request.Name, request.Color);

        if (!result.Succeeded)
            return BadRequest(ApiResponse<string>.ValidationFailed(result.Errors));

        return Ok(ApiResponse<object>.Success(result.Data!, "Crate created successfully"));
    }

    [HttpGet]
    public async Task<IActionResult> GetCrates()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(ApiResponse<string>.Unauthorized("You do not have permission to access this resource"));

        var crates = await _crateService.GetCratesAsync(userId);
        return Ok(ApiResponse<object>.Success(crates, "Crates retrieved successfully"));
    }

    [HttpDelete("{crateId:guid}")]
    public async Task<IActionResult> DeleteCrate(Guid crateId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(ApiResponse<string>.Unauthorized("You do not have permission to access this resource"));

        var result = await _crateService.DeleteCrateAsync(crateId, userId);

        if (!result.Succeeded)
            return NotFound(ApiResponse<string>.Error(result.Errors[0].Message, 404));

        return Ok(ApiResponse<object>.SuccessMessage("Crate deleted successfully"));
    }
}