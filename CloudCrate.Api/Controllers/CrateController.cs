using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CloudCrate.Api.Requests.Crate;
using CloudCrate.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudCrate.Api.Controllers;

[ApiController]
[Route("api/crates")]
[Authorize]
public class CrateController : ControllerBase
{
    private readonly ICrateService _crateService;

    public CrateController(ICrateService crateService)
    {
        _crateService = crateService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateCrate([FromBody] CreateCrateRequest request)
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(ApiResponse<string>.WithMessage("Invalid user"));

        var result = await _crateService.CreateCrateAsync(userId, request.Name);

        if (!result.Succeeded)
            return BadRequest(ApiResponse<string>.WithErrors(result.Errors[0].Message, 400, result.Errors));

        return Ok(ApiResponse<object>.WithData(result.Data!, "Crate created successfully"));
    }

    [HttpGet]
    public async Task<IActionResult> GetCrates()
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(ApiResponse<string>.WithMessage("Invalid user"));

        var crates = await _crateService.GetCratesAsync(userId);
        return Ok(ApiResponse<object>.WithData(crates, "Crates retrieved successfully"));
    }

    [HttpDelete("{crateId:guid}")]
    public async Task<IActionResult> DeleteCrate(Guid crateId)
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(ApiResponse<string>.WithMessage("Invalid user"));

        var result = await _crateService.DeleteCrateAsync(crateId, userId);

        if (!result.Succeeded)
            return NotFound(ApiResponse<string>.WithErrors(result.Errors[0].Message, 404, result.Errors));

        return Ok(ApiResponse<object>.WithMessage("Crate deleted successfully"));
    }
}