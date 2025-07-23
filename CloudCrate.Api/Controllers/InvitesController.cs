using System.Security.Claims;
using CloudCrate.Api.Common.Extensions;
using CloudCrate.Api.Models;
using CloudCrate.Application.Interfaces.Crate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudCrate.Api.Controllers
{
    [ApiController]
    [Route("api/invites")]
    public class InvitesController : ControllerBase
    {
        private readonly ICrateInviteService _inviteService;
        private readonly ICrateUserRoleService _roleService;

        public InvitesController(ICrateInviteService inviteService, ICrateUserRoleService roleService)
        {
            _inviteService = inviteService;
            _roleService = roleService;
        }

        [HttpGet("token/{token}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetInviteByToken(string token)
        {
            var result = await _inviteService.GetInviteByTokenAsync(token);
            return result.ToActionResult(this, successMessage: "Invite retrieved");
        }

        [HttpPost("token/{token}/accept")]
        [Authorize]
        public async Task<IActionResult> AcceptInvite(string token)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(ApiResponse<object>.Unauthorized("You must be logged in to accept invites."));

            var result = await _inviteService.AcceptInviteAsync(token, userId, _roleService);
            return result.ToActionResult(this, successMessage: "Invite accepted");
        }

        [HttpPost("token/{token}/decline")]
        [Authorize]
        public async Task<IActionResult> DeclineInvite(string token)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(ApiResponse<object>.Unauthorized("You must be logged in to decline invites."));

            var result = await _inviteService.DeclineInviteAsync(token);
            return result.ToActionResult(this, successMessage: "Invite declined");
        }
    }
}