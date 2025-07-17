using CloudCrate.Application.Common.Interfaces;
using CloudCrate.Domain.Enums;
using CloudCrate.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CloudCrate.Application.DTOs.Roles;
using Microsoft.AspNetCore.Identity;
using CloudCrate.Api.Models;
using CloudCrate.Application.Common.Errors;

namespace CloudCrate.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/crates/{crateId:guid}/roles")]
public class CrateRolesController : ControllerBase
{
    private readonly ICrateUserRoleService _roleService;
    private readonly UserManager<ApplicationUser> _userManager;

    public CrateRolesController(ICrateUserRoleService roleService, UserManager<ApplicationUser> userManager)
    {
        _roleService = roleService;
        _userManager = userManager;
    }

    [HttpPut]
    public async Task<IActionResult> AssignRole(Guid crateId, [FromBody] AssignRoleRequest request)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized(ApiResponse<string>.Unauthorized("You are not authorized"));

        if (!await _roleService.IsOwnerAsync(crateId, currentUserId))
            return StatusCode(403, ApiResponse<string>.Forbidden("Only the owner can assign roles"));

        if (request.UserId == currentUserId && request.Role != CrateRole.Owner)
        {
            return BadRequest(
                ApiResponse<string>.ValidationFailed(new List<Error> { Errors.OwnerRoleRemovalNotAllowed }));
        }

        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user == null)
            return NotFound(ApiResponse<string>.NotFound("Target user not found"));

        await _roleService.AssignRoleAsync(crateId, request.UserId, request.Role);
        return Ok(ApiResponse<object>.SuccessMessage("Role assigned successfully"));
    }
}