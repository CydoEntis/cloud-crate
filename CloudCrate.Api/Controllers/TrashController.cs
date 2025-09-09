using CloudCrate.Application.DTOs.Pagination;
using CloudCrate.Application.DTOs.Trash;
using CloudCrate.Application.Interfaces.Trash;
using CloudCrate.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudCrate.Api.Controllers;

[ApiController]
[Route("api/crates/{crateId:guid}/trash")]
[Authorize]
public class TrashController : BaseController
{
    private readonly ITrashService _trashService;

    public TrashController(ITrashService trashService)
    {
        _trashService = trashService;
    }


    [HttpGet]
    public async Task<IActionResult> GetDeletedItems(Guid crateId, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _trashService.FetchDeletedItemsAsync(crateId, UserId!, page, pageSize);
        return Response(
            ApiResponse<PaginatedResult<TrashItemResponse>>.Success(result, "Deleted items retrieved successfully"));
    }

    [HttpPost("restore")]
    public async Task<IActionResult> RestoreItems([FromBody] RestoreTrashRequest request)
    {
        var result = await _trashService.RestoreItemsAsync(request.FileIds, request.FolderIds, UserId!);
        return Response(ApiResponse.FromResult(result, "Items restored successfully"));
    }


    [HttpDelete("permanent")]
    public async Task<IActionResult> PermanentlyDeleteItems([FromBody] DeleteTrashRequest request)
    {
        var result = await _trashService.PermanentlyDeleteItemsAsync(request.FileIds, request.FolderIds, UserId!);
        return Response(ApiResponse.FromResult(result, "Items permanently deleted successfully"));
    }
}