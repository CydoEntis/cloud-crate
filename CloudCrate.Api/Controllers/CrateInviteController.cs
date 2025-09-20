using CloudCrate.Api.Models;
using CloudCrate.Application.DTOs.Invite.Request;
using CloudCrate.Application.Interfaces.Crate;
using CloudCrate.Application.DTOs.Invite.Response;
using CloudCrate.Api.Common.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudCrate.Api.Controllers;

[ApiController]
[Route("api/invite")]
[Authorize]
public class CrateInviteController : BaseController
{
    private readonly ICrateInviteService _inviteService;

    public CrateInviteController(ICrateInviteService inviteService)
    {
        _inviteService = inviteService;
    }

    [HttpPost]
    public async Task<IActionResult> SendInvite([FromBody] CrateInviteRequest request)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse<EmptyResponse>.Failure("User is not authenticated", 401));
        }

        var result = await _inviteService.CreateInviteAsync(UserId, request);
        
        if (result.IsSuccess)
        {
            return Created("", ApiResponse<EmptyResponse>.Success(
                message: "Invite sent successfully",
                statusCode: 201));
        }

        return result.GetError().ToActionResult<EmptyResponse>();
    }

    [HttpGet("token/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetInviteByToken(string token)
    {
        var result = await _inviteService.GetInviteDetailsByTokenAsync(token);
        
        if (result.IsSuccess)
        {
            return Ok(ApiResponse<CrateInviteDetailsResponse>.Success(
                data: result.GetValue(),
                message: "Invite retrieved successfully"));
        }
        
        return result.GetError().ToActionResult<CrateInviteDetailsResponse>();
    }

    [HttpPost("token/{token}/accept")]
    public async Task<IActionResult> AcceptInvite(string token)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse<EmptyResponse>.Failure("User is not authenticated", 401));
        }

        var result = await _inviteService.AcceptInviteAsync(token, UserId);
        
        if (result.IsSuccess)
        {
            return Ok(ApiResponse<EmptyResponse>.Success(message: "Invite accepted successfully"));
        }
        
        return result.GetError().ToActionResult<EmptyResponse>();
    }

    [HttpPost("token/{token}/decline")]
    public async Task<IActionResult> DeclineInvite(string token)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse<EmptyResponse>.Failure("User is not authenticated", 401));
        }

        var result = await _inviteService.DeclineInviteAsync(token);
        
        if (result.IsSuccess)
        {
            return Ok(ApiResponse<EmptyResponse>.Success(message: "Invite declined successfully"));
        }
        
        return result.GetError().ToActionResult<EmptyResponse>();
    }
}