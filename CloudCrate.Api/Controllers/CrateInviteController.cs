using CloudCrate.Api.Models;
using CloudCrate.Application.DTOs.Invite.Request;
using CloudCrate.Application.Interfaces.Crate;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.Invite.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudCrate.Api.Controllers;

[ApiController]
[Route("api/invite")]
[Authorize]
public class CrateInviteController : BaseController
{
    private readonly ICrateInviteService _inviteService;
    private readonly ICrateMemberService _roleService;

    public CrateInviteController(ICrateInviteService inviteService, ICrateMemberService roleService)
    {
        _inviteService = inviteService;
        _roleService = roleService;
    }

    [HttpPost]
    public async Task<IActionResult> SendInvite([FromBody] CrateInviteRequest request)
    {
        var unauthorized = EnsureUserAuthenticated();
        if (unauthorized != null) return unauthorized;

        var result = await _inviteService.CreateInviteAsync(
            request.CrateId, request.Email, UserId!, request.Role, request.ExpiresAt);

        return Response(ApiResponse<Result>.Success(result, "Invite sent", 201));
    }

    [HttpGet("token/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetInviteByToken(string token)
    {
        var result = await _inviteService.GetInviteByTokenAsync(token);
        return Response(ApiResponse<CrateInviteDetailsResponse>.FromResult(result, "Invite retrieved", 200));
    }

    [HttpPost("token/{token}/accept")]
    public async Task<IActionResult> AcceptInvite(string token)
    {
        var unauthorized = EnsureUserAuthenticated();
        if (unauthorized != null) return unauthorized;

        var result = await _inviteService.AcceptInviteAsync(token, UserId!, _roleService);
        return Response(ApiResponse.FromResult(result, "Invite accepted", 200));
    }

    [HttpPost("token/{token}/decline")]
    public async Task<IActionResult> DeclineInvite(string token)
    {
        var unauthorized = EnsureUserAuthenticated();
        if (unauthorized != null) return unauthorized;

        var result = await _inviteService.DeclineInviteAsync(token);
        return Response(ApiResponse.FromResult(result, "Invite declined", 200));
    }
}