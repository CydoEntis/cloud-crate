using CloudCrate.Api.Models;
using CloudCrate.Application.DTOs.Crate.Response;
using CloudCrate.Application.DTOs.Roles.Request;
using CloudCrate.Application.Interfaces.Crate;
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
    public async Task<IActionResult> GetMembers(Guid crateId)
    {
        var userId = UserId!;
        var result = await _crateMemberService.GetMembersForCrateAsync(crateId, userId);
        return Response(ApiResponse<List<CrateMemberResponse>>.FromResult(result));
    }

    [HttpPut("{userId:guid}/role")]
    public async Task<IActionResult> AssignRole(Guid crateId, string userId, [FromBody] AssignRoleRequest request)
    {
        var requestingUserId = UserId!;
        var result = await _crateMemberService.AssignRoleAsync(crateId, userId, request.Role, requestingUserId);
        return Response(ApiResponse.FromResult(result,
            result.IsSuccess ? "Role assigned successfully" : result.Error?.Message,
            result.IsSuccess ? 204 : 400));
    }

    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid crateId, string userId)
    {
        var requestingUserId = UserId!;
        var result = await _crateMemberService.RemoveMemberAsync(crateId, userId, requestingUserId);
        return Response(ApiResponse.FromResult(result,
            result.IsSuccess ? "Member removed" : result.Error?.Message,
            result.IsSuccess ? 204 : 400));
    }

    [HttpPost("leave")]
    public async Task<IActionResult> LeaveCrate(Guid crateId)
    {
        var userId = UserId!;
        var result = await _crateMemberService.LeaveCrateAsync(crateId, userId);
        return Response(ApiResponse.FromResult(result,
            result.IsSuccess ? "Left crate" : result.Error?.Message,
            result.IsSuccess ? 204 : 400));
    }

    [HttpPost("bulk-leave")]
    public async Task<IActionResult> LeaveCrates([FromBody] IEnumerable<Guid> crateIds)
    {
        var userId = UserId!;
        var result = await _crateMemberService.LeaveCratesAsync(crateIds, userId);
        return Response(ApiResponse<int>.FromResult(result,
            result.IsSuccess ? $"{result.Value} crates left" : result.Error?.Message,
            result.IsSuccess ? 204 : 400));
    }
}