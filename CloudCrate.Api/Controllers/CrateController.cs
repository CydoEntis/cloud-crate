using CloudCrate.Application.DTOs.Crate.Request;
using CloudCrate.Application.DTOs.Crate.Response;
using CloudCrate.Application.DTOs.Pagination;
using CloudCrate.Application.Interfaces.Crate;
using CloudCrate.Api.Models;
using CloudCrate.Api.Common.Extensions;
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
        var result = await _crateService.CreateCrateAsync(request);

        if (result.IsSuccess)
        {
            return Created("", ApiResponse<Guid>.Success(
                data: result.GetValue(),
                message: "Crate created successfully",
                statusCode: 201));
        }

        return result.GetError().ToActionResult<Guid>();
    }

    [HttpGet]
    public async Task<IActionResult> GetCrates([FromQuery] CrateQueryParameters queryParameters)
    {
        var result = await _crateService.GetCratesAsync(UserId, queryParameters);

        if (result.IsSuccess)
        {
            return Ok(ApiResponse<PaginatedResult<CrateSummaryResponse>>.Success(
                data: result.GetValue(),
                message: "Crates retrieved successfully"));
        }

        return result.GetError().ToActionResult<PaginatedResult<CrateSummaryResponse>>();
    }

    [HttpPut("{crateId:guid}")]
    public async Task<IActionResult> UpdateCrate(Guid crateId, [FromBody] UpdateCrateRequest request)
    {
        var result = await _crateService.UpdateCrateAsync(crateId, UserId!, request);

        if (result.IsSuccess)
        {
            return Ok(ApiResponse<EmptyResponse>.Success(message: "Crate updated successfully"));
        }

        return result.GetError().ToActionResult<EmptyResponse>();
    }

    [HttpGet("{crateId:guid}")]
    public async Task<IActionResult> GetCrate(Guid crateId)
    {
        var result = await _crateService.GetCrateAsync(crateId, UserId!);

        if (result.IsSuccess)
        {
            return Ok(ApiResponse<CrateDetailsResponse>.Success(
                data: result.GetValue(),
                message: "Crate retrieved successfully"));
        }

        return result.GetError().ToActionResult<CrateDetailsResponse>();
    }

    [HttpDelete("{crateId:guid}")]
    public async Task<IActionResult> DeleteCrate(Guid crateId)
    {
        var result = await _crateService.DeleteCrateAsync(crateId, UserId!);

        if (result.IsSuccess)
        {
            return NoContent();
        }

        return result.GetError().ToActionResult<EmptyResponse>();
    }

}