using System.Security.Claims;
using CloudCrate.Api.Common.Extensions;
using CloudCrate.Api.Models;
using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.DTOs.Crate.Request;
using CloudCrate.Application.Interfaces.Crate;
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

        var result = await _crateService.CreateCrateAsync(userId, request.Name, request.Color, request.AllocatedStorageGb);

        return result.ToActionResult(this, 201, "Crate created successfully");
    }

    [HttpGet]
    public async Task<IActionResult> GetCrates([FromQuery] CrateQueryParameters queryParameters)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(ApiResponse<string>.Unauthorized("You do not have permission to access this resource"));

        queryParameters.UserId = userId;

        var result = await _crateService.GetCratesAsync(queryParameters);

        return result.ToActionResult(this, successMessage: "Crates retrieved successfully");
    }

    [HttpPut("{crateId:guid}")]
    public async Task<IActionResult> UpdateCrate(Guid crateId, [FromBody] UpdateCrateRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage).ToList();

            return BadRequest(ApiResponse<string>.ValidationFailed(
                errors.Select(msg => new Error("ERR_VALIDATION", msg)).ToList()));
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(ApiResponse<string>.Unauthorized("You do not have permission to access this resource"));

        var result = await _crateService.UpdateCrateAsync(crateId, userId, request.Name, request.Color);

        return result.ToActionResult(this, successMessage: "Crate updated successfully");
    }

    [HttpDelete("{crateId:guid}")]
    public async Task<IActionResult> DeleteCrate(Guid crateId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(ApiResponse<string>.Unauthorized("You do not have permission to access this resource"));

        var result = await _crateService.DeleteCrateAsync(crateId, userId);

        return result.ToActionResult(this, successMessage: "Crate deleted successfully");
    }

    [HttpGet("{crateId:guid}")]
    public async Task<IActionResult> GetCrate(Guid crateId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(ApiResponse<string>.Unauthorized("You do not have permission to access this resource"));

        var result = await _crateService.GetCrateAsync(crateId, userId);

        return result.ToActionResult(this, successMessage: "Crate retrieved successfully");
    }

    [HttpGet("{crateId:guid}/members")]
    public async Task<IActionResult> GetCrateMembers(
        Guid crateId,
        [FromQuery] CrateMemberRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(ApiResponse<string>.Unauthorized("You do not have permission to access this resource"));

        var result = await _crateService.GetCrateMembersAsync(crateId, request);

        return result.ToActionResult(this, successMessage: "Crate members retrieved successfully");
    }

    [HttpDelete("{crateId:guid}/leave")]
    public async Task<IActionResult> LeaveCrate(Guid crateId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(ApiResponse<string>.Unauthorized("You do not have permission to access this resource"));

        var result = await _crateService.LeaveCrateAsync(crateId, userId);

        return result.ToActionResult(this, successMessage: "You have left the crate successfully");
    }
}