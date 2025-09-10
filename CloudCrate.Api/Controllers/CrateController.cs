using CloudCrate.Application.DTOs.Crate.Request;
using CloudCrate.Application.DTOs.Crate.Response;
using CloudCrate.Application.DTOs.Pagination;
using CloudCrate.Application.Interfaces.Crate;
using CloudCrate.Api.Models;
using CloudCrate.Application.DTOs.User.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudCrate.Api.Controllers;

[ApiController]
[Route("api/crates")]
[Authorize]
public class CrateController : BaseController
{
    private readonly ICrateService _crateService;

    public CrateController(ICrateService crateService)
    {
        _crateService = crateService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateCrate([FromBody] CreateCrateRequest request)
    {
        request.UserId = UserId;
        var result =
            await _crateService.CreateCrateAsync(request);
        return Response(ApiResponse<Guid>.FromResult(result, "Crate created successfully", 201));
    }

    [HttpGet]
    public async Task<IActionResult> GetCrates([FromQuery] CrateQueryParameters queryParameters)
    {
        queryParameters.UserId = UserId!;
        var result = await _crateService.GetCratesAsync(queryParameters);
        return Response(
            ApiResponse<PaginatedResult<CrateListItemResponse>>.FromResult(result, "Crates retrieved successfully"));
    }

    [HttpPut("{crateId:guid}")]
    public async Task<IActionResult> UpdateCrate(Guid crateId, [FromBody] UpdateCrateRequest request)
    {
        var result = await _crateService.UpdateCrateAsync(crateId, UserId!, request.Name, request.Color);
        return Response(ApiResponse<CrateListItemResponse>.FromResult(result, "Crate updated successfully"));
    }


    [HttpGet("{crateId:guid}")]
    public async Task<IActionResult> GetCrate(Guid crateId)
    {
        var result = await _crateService.GetCrateAsync(crateId, UserId!);
        return Response(ApiResponse<CrateDetailsResponse>.FromResult(result, "Crate retrieved successfully"));
    }

    [HttpGet("{crateId:guid}/members")]
    public async Task<IActionResult> GetCrateMembers(Guid crateId, [FromQuery] CrateMemberRequest request)
    {
        var result = await _crateService.GetCrateMembersAsync(crateId, request);
        return Response(
            ApiResponse<List<CrateMemberResponse>>.FromResult(result, "Crate members retrieved successfully"));
    }

    [HttpDelete("{crateId:guid}")]
    public async Task<IActionResult> DeleteCrate(Guid crateId)
    {
        var result = await _crateService.DeleteCrateAsync(crateId);
        return Response(ApiResponse.FromResult(result, "Crate deleted successfully", 204));
    }

    [HttpPost("bulk-delete")]
    public async Task<IActionResult> BulkDeleteCrates([FromBody] BulkCrateActionRequest request)
    {
        if (request.CrateIds == null || !request.CrateIds.Any())
            return BadRequest("No crate IDs provided.");

        var result = await _crateService.DeleteCratesAsync(request.CrateIds);
        return Response(ApiResponse.FromResult(result, $"{request.CrateIds.Count} crates deleted successfully", 200));
    }

    [HttpDelete("{crateId:guid}/leave")]
    public async Task<IActionResult> LeaveCrate(Guid crateId)
    {
        var result = await _crateService.LeaveCrateAsync(crateId, UserId!);
        return Response(ApiResponse.FromResult(result, "You have left the crate successfully", 204));
    }


    [HttpPost("bulk-leave")]
    public async Task<IActionResult> BulkLeaveCrates([FromBody] BulkCrateActionRequest request)
    {
        if (request.CrateIds == null || !request.CrateIds.Any())
            return BadRequest("No crate IDs provided.");

        var result = await _crateService.BulkLeaveCratesAsync(request.CrateIds, UserId!);
        return Response(ApiResponse<int>.FromResult(result, $"Left {result.Value} crates successfully", 200));
    }
}