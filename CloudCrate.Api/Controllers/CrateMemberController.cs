using CloudCrate.Domain.Enums;
using CloudCrate.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using CloudCrate.Api.Models;
using CloudCrate.Api.Common.Extensions;
using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.DTOs.Roles.Request;
using CloudCrate.Application.Interfaces.Crate;
using CloudCrate.Application.Interfaces.Permissions;

namespace CloudCrate.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/crates/{crateId:guid}/roles")]
public class CrateMemberController : ControllerBase
{
    private readonly ICrateMemberService _crateMemberService;
    private readonly ICrateRoleService _roleService;
    private readonly UserManager<ApplicationUser> _userManager;

    public CrateMemberController(
        ICrateMemberService crateMemberService,
        ICrateRoleService roleService,
        UserManager<ApplicationUser> userManager)
    {
        _crateMemberService = crateMemberService;
        _roleService = roleService;
        _userManager = userManager;
    }

    [HttpPut]
    public async Task<IActionResult> AssignRole(Guid crateId, [FromBody] AssignRoleRequest request)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized(ApiResponse<object>.Unauthorized("You are not authorized"));

        // ✅ Use permission service to check if current user is owner
        var canManageResult = await _roleService.CanManageCrate(crateId, currentUserId);
        if (!canManageResult.Succeeded || !canManageResult.Value)
            return StatusCode(403, ApiResponse<object>.Forbidden("Only the owner can assign roles"));

        // Prevent self-demotion
        if (request.UserId == currentUserId && request.Role != CrateRole.Owner)
        {
            var error = Errors.Roles.Validation(
                "CrateRole.SelfDemotion",
                "You cannot demote yourself from the owner role."
            );
            return BadRequest(ApiResponse<string>.ValidationFailed(new List<Error> { error }));
        }

        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user == null)
            return NotFound(ApiResponse<string>.NotFound("Target user not found"));

        var result = await _crateMemberService.AssignRoleAsync(crateId, request.UserId, request.Role);
        return result.ToActionResult(this, 204, "Role assigned successfully");
    }
}