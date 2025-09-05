using CloudCrate.Domain.Enums;
using CloudCrate.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using CloudCrate.Api.Models;
using CloudCrate.Application.DTOs.Roles.Request;
using CloudCrate.Application.Interfaces.Crate;
using CloudCrate.Application.Interfaces.Permissions;

namespace CloudCrate.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/crates/{crateId:guid}/roles")]
public class CrateMemberController : BaseController
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
        var unauthorized = EnsureUserAuthenticated();
        if (unauthorized != null) return unauthorized;

        var result = await _crateMemberService.AssignRoleAsync(crateId, request.UserId, request.Role);

        return Response(ApiResponse.FromResult(result,
            result.IsSuccess ? "Role assigned successfully" : result.Error?.Message, result.IsSuccess ? 204 : 400));
    }
}