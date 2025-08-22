using System.Security.Claims;
using CloudCrate.Application.DTOs;
using CloudCrate.Application.Interfaces.Folder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/crates/{crateId:guid}/bulk")]
[Authorize]
public class BulkController : ControllerBase
{
    private readonly IFolderService _folderService;

    public BulkController(IFolderService folderService)
    {
        _folderService = folderService;
    }

    private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpPost("delete")]
    public async Task<IActionResult> DeleteMultiple(Guid crateId, [FromBody] MultipleDeleteRequest request)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized("You do not have permission to access this resource");

        var result = await _folderService.DeleteMultipleAsync(request, userId!);
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        return Ok("Selected files and folders deleted successfully");
    }
}