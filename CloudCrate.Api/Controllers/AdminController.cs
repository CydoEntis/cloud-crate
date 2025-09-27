using CloudCrate.Api.Common.Extensions;
using CloudCrate.Api.Controllers;
using CloudCrate.Api.Models;
using CloudCrate.Application.DTOs.Admin.Request;
using CloudCrate.Application.DTOs.Admin.Response;
using CloudCrate.Application.DTOs.Invite.Request;
using CloudCrate.Application.DTOs.Invite.Response;
using CloudCrate.Application.DTOs.Pagination;
using CloudCrate.Application.Interfaces.Admin;
using CloudCrate.Application.Interfaces.Invite;
using CloudCrate.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/admin")]
[Authorize]
public class AdminController : BaseController
{
    private readonly IAdminService _adminService;
    private readonly IUserInviteService _inviteService;

    public AdminController(IAdminService adminService, IUserInviteService inviteService)
    {
        _adminService = adminService;
        _inviteService = inviteService;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetAdminStatus()
    {
        var result = await _adminService.IsUserAdminAsync(UserId!);

        if (result.IsSuccess)
        {
            return Ok(ApiResponse<object>.Success(
                data: new { IsAdmin = result.GetValue() },
                message: "Admin status retrieved"));
        }

        return result.GetError().ToActionResult<object>();
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] AdminUserParameters parameters)
    {
        if (Request.Query.TryGetValue("sortBy", out var sortByValue))
        {
            if (Enum.TryParse<AdminUserSortBy>(sortByValue.ToString(), true, out var parsedSortBy))
            {
                parameters.SortBy = parsedSortBy;
            }
        }

        if (Request.Query.TryGetValue("userType", out var userTypeValue))
        {
            if (Enum.TryParse<AdminUserType>(userTypeValue.ToString(), true, out var parsedUserType))
            {
                parameters.UserType = parsedUserType;
            }
        }

        if (Request.Query.TryGetValue("userStatus", out var userStatusValue))
        {
            if (Enum.TryParse<AdminUserStatus>(userStatusValue.ToString(), true, out var parsedUserStatus))
            {
                parameters.UserStatus = parsedUserStatus;
            }
        }

        if (Request.Query.TryGetValue("planFilter", out var planFilterValue))
        {
            if (Enum.TryParse<AdminPlanFilter>(planFilterValue.ToString(), true, out var parsedPlanFilter))
            {
                parameters.PlanFilter = parsedPlanFilter;
            }
        }

        var result = await _adminService.GetUsersAsync(parameters);

        if (result.IsSuccess)
        {
            return Ok(ApiResponse<PaginatedResult<AdminUserResponse>>.Success(
                data: result.GetValue(),
                message: "Users retrieved successfully"));
        }

        return result.GetError().ToActionResult<PaginatedResult<AdminUserResponse>>();
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetAdminStats()
    {
        var result = await _adminService.GetAdminStatsAsync(UserId!);

        if (result.IsSuccess)
        {
            return Ok(ApiResponse<AdminStatsResponse>.Success(
                data: result.GetValue(),
                message: "Admin stats retrieved successfully"));
        }

        return result.GetError().ToActionResult<AdminStatsResponse>();
    }

    [HttpPost("users/{userId}/ban")]
    public async Task<IActionResult> BanUser(string userId)
    {
        var result = await _adminService.BanUserAsync(UserId!, userId);

        if (result.IsSuccess)
        {
            return Ok(ApiResponse<EmptyResponse>.Success(message: "User banned successfully"));
        }

        return result.GetError().ToActionResult<EmptyResponse>();
    }

    [HttpPost("users/{userId}/unban")]
    public async Task<IActionResult> UnbanUser(string userId)
    {
        var result = await _adminService.UnbanUserAsync(UserId!, userId);

        if (result.IsSuccess)
        {
            return Ok(ApiResponse<EmptyResponse>.Success(message: "User unbanned successfully"));
        }

        return result.GetError().ToActionResult<EmptyResponse>();
    }


    [HttpPost("users/{userId}/make-admin")]
    public async Task<IActionResult> MakeUserAdmin(string userId)
    {
        var result = await _adminService.PromoteToAdminAsync(UserId!, userId);

        if (result.IsSuccess)
        {
            return Ok(ApiResponse<EmptyResponse>.Success(message: "User promoted to admin successfully"));
        }

        return result.GetError().ToActionResult<EmptyResponse>();
    }

    [HttpPost("users/{userId}/remove-admin")]
    public async Task<IActionResult> RemoveUserAdmin(string userId)
    {
        var result = await _adminService.RemoveAdminAsync(UserId!, userId);

        if (result.IsSuccess)
        {
            return Ok(ApiResponse<EmptyResponse>.Success(message: "Admin privileges removed successfully"));
        }

        return result.GetError().ToActionResult<EmptyResponse>();
    }

    [HttpPost("users/{userId}/plan")]
    public async Task<IActionResult> UpdateUserPlan(string userId, [FromBody] UpdateUserPlanRequest request)
    {
        var result = await _adminService.UpdateUserPlanAsync(UserId!, userId, request.Plan);

        if (result.IsSuccess)
        {
            return Ok(ApiResponse<EmptyResponse>.Success(message: "User plan updated successfully"));
        }

        return result.GetError().ToActionResult<EmptyResponse>();
    }

    [HttpPost("invites")]
    public async Task<IActionResult> CreateInvite([FromBody] CreateUserInviteRequest request)
    {
        var result = await _inviteService.CreateInviteAsync(UserId!, request);

        if (result.IsSuccess)
        {
            return Created("", ApiResponse<InviteUserResponse>.Success(
                data: result.GetValue(),
                message: "Invite created successfully",
                statusCode: 201));
        }

        return result.GetError().ToActionResult<InviteUserResponse>();
    }

    [HttpGet("invites")]
    public async Task<IActionResult> GetAllInvites()
    {
        var result = await _inviteService.GetInvitesByUserAsync(UserId!);

        if (result.IsSuccess)
        {
            return Ok(ApiResponse<IEnumerable<InviteUserResponse>>.Success(
                data: result.GetValue(),
                message: "Invites retrieved successfully"));
        }

        return result.GetError().ToActionResult<IEnumerable<InviteUserResponse>>();
    }

    [HttpDelete("invites/expired")]
    public async Task<IActionResult> DeleteExpiredInvites()
    {
        var result = await _inviteService.DeleteExpiredInvitesAsync();

        if (result.IsSuccess)
        {
            return Ok(ApiResponse<EmptyResponse>.Success(message: "Expired invites deleted successfully"));
        }

        return result.GetError().ToActionResult<EmptyResponse>();
    }
}