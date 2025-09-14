using CloudCrate.Api.Models;
using CloudCrate.Application.DTOs.Crate.Response;
using CloudCrate.Application.DTOs.Roles.Request;
using CloudCrate.Application.Interfaces.Crate;
using CloudCrate.Api.Common.Extensions;
using CloudCrate.Application.DTOs.CrateMember.Request;
using CloudCrate.Application.DTOs.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudCrate.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/crates/{crateId:guid}/members")]
public class CrateMemberController : BaseController
{
    private readonly ICrateMemberService _crateMemberService;

    public CrateMemberController(ICrateMemberService crateMemberService)
    {
        _crateMemberService = crateMemberService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMembers(Guid crateId, CrateMemberQueryParameters queryParameters)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse<PaginatedResult<CrateMemberResponse>>.Failure(
                "User is not authenticated", 401));
        }
    
        var result = await _crateMemberService.GetCrateMembersAsync(crateId, UserId, queryParameters);
    
        if (result.IsSuccess)
        {
            return Ok(ApiResponse<PaginatedResult<CrateMemberResponse>>.Success(
                data: result.GetValue(),
                message: "Members retrieved successfully"));
        }
    
        return result.GetError().ToActionResult<PaginatedResult<CrateMemberResponse>>();
    }

    [HttpPut("{userId:guid}/role")]
    public async Task<IActionResult> AssignRole(Guid crateId, string userId, [FromBody] AssignRoleRequest request)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse<EmptyResponse>.Failure("User is not authenticated", 401));
        }

        var result = await _crateMemberService.AssignRoleAsync(crateId, userId, request.Role, UserId);

        if (result.IsSuccess)
        {
            return NoContent();
        }

        return result.GetError().ToActionResult<EmptyResponse>();
    }

    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid crateId, string userId)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse<EmptyResponse>.Failure("User is not authenticated", 401));
        }

        var result = await _crateMemberService.RemoveMemberAsync(crateId, userId, UserId);

        if (result.IsSuccess)
        {
            return NoContent();
        }

        return result.GetError().ToActionResult<EmptyResponse>();
    }

    [HttpPost("leave")]
    public async Task<IActionResult> LeaveCrate(Guid crateId)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse<EmptyResponse>.Failure("User is not authenticated", 401));
        }

        var result = await _crateMemberService.LeaveCrateAsync(crateId, UserId);

        if (result.IsSuccess)
        {
            return NoContent();
        }

        return result.GetError().ToActionResult<EmptyResponse>();
    }

    [HttpPost("bulk-leave")]
    public async Task<IActionResult> LeaveCrates([FromBody] IEnumerable<Guid> crateIds)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse<int>.Failure("User is not authenticated", 401));
        }

        var result = await _crateMemberService.LeaveCratesAsync(crateIds, UserId);

        if (result.IsSuccess)
        {
            var leftCount = result.GetValue();
            return Ok(ApiResponse<int>.Success(
                data: leftCount,
                message: $"Successfully left {leftCount} crates"));
        }

        return result.GetError().ToActionResult<int>();
    }
}