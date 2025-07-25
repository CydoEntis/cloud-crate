﻿using CloudCrate.Domain.Enums;
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
            return Unauthorized(ApiResponse<object>.Unauthorized("You are not authorized"));

        var isOwnerResult = await _roleService.IsOwnerAsync(crateId, currentUserId);
        if (!isOwnerResult.Succeeded || !isOwnerResult.Value)
            return StatusCode(403, ApiResponse<object>.Forbidden("Only the owner can assign roles"));

        if (request.UserId == currentUserId && request.Role != CrateRole.Owner)
        {
            var error = Errors.Roles.Validation("CrateRole.SelfDemotion",
                "You cannot demote yourself from the owner role.");
            return BadRequest(ApiResponse<string>.ValidationFailed(new List<Error> { error }));
        }

        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user == null)
            return NotFound(ApiResponse<string>.NotFound("Target user not found"));

        var result = await _roleService.AssignRoleAsync(crateId, request.UserId, request.Role);

        return result.ToActionResult(this, 204, "Role assigned successfully");
    }
}