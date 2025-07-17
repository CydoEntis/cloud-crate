using System.Security.Claims;
using CloudCrate.Api.Models;
using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Interfaces;
using CloudCrate.Application.DTOs.Invites;
using CloudCrate.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudCrate.Api.Controllers
{
    [ApiController]
    [Route("api/crates/{crateId:guid}/invites")]
    [Authorize]
    public class CrateInvitesController : ControllerBase
    {
        private readonly ICrateInviteService _inviteService;
        private readonly ICrateUserRoleService _roleService;

        public CrateInvitesController(ICrateInviteService inviteService, ICrateUserRoleService roleService)
        {
            _inviteService = inviteService;
            _roleService = roleService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateInvite(Guid crateId, [FromBody] CrateInviteRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(ApiResponse<object>.Unauthorized());

            var result = await _inviteService.CreateInviteAsync(crateId, request.Email, userId, request.Role);

            if (result.Succeeded)
                return Ok(ApiResponse<string>.Success($"Invite sent to {request.Email}"));

            return BadRequest(ApiResponse<List<Error>>.ValidationFailed(result.Errors));
        }


        [HttpGet("token/{token}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetInviteByToken(string token)
        {
            var result = await _inviteService.GetInviteByTokenAsync(token);

            if (result.Succeeded)
                return Ok(ApiResponse<CrateInvite?>.Success(result.Data));

            return NotFound(ApiResponse<List<Error>>.ValidationFailed(result.Errors));
        }

        [HttpPost("token/{token}/accept")]
        public async Task<IActionResult> AcceptInvite(string token)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(ApiResponse<object>.Unauthorized("You must be logged in to accept invites."));

            var result = await _inviteService.AcceptInviteAsync(token, userId, _roleService);

            if (result.Succeeded)
                return Ok(ApiResponse<object>.SuccessMessage("Invite accepted"));

            return BadRequest(ApiResponse<List<Error>>.ValidationFailed(result.Errors));
        }

        [HttpPost("token/{token}/decline")]
        public async Task<IActionResult> DeclineInvite(string token)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(ApiResponse<object>.Unauthorized("You must be logged in to decline invites."));

            var result = await _inviteService.DeclineInviteAsync(token);

            if (result.Succeeded)
                return Ok(ApiResponse<object>.SuccessMessage("Invite declined"));

            return BadRequest(ApiResponse<List<Error>>.ValidationFailed(result.Errors));
        }
    }
}