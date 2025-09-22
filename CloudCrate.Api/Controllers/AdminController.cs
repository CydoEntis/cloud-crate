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

    [HttpDelete("users/{userId}")]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        var result = await _adminService.DeleteUserAsync(UserId!, userId);

        if (result.IsSuccess)
        {
            return NoContent();
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