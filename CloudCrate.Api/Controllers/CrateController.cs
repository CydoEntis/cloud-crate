// using CloudCrate.Api.Requests.Crate;
// using CloudCrate.Application.Common.Interfaces;
// using CloudCrate.Infrastructure.Identity;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Identity;
// using Microsoft.AspNetCore.Mvc;
//
// namespace CloudCrate.Api.Controllers;
//
// [Authorize]
// [ApiController]
// [Route("api/[controller]")]
// public class CrateController : ControllerBase
// {
//     private readonly ICrateService _crateService;
//     private readonly UserManager<ApplicationUser> _userManager;
//
//     public CrateController(ICrateService crateService, UserManager<ApplicationUser> userManager)
//     {
//         _crateService = crateService;
//         _userManager = userManager;
//     }
//
//     private async Task<ApplicationUser?> GetCurrentUserAsync() =>
//         await _userManager.GetUserAsync(User);
//
//     [HttpPost]
//     public async Task<IActionResult> CreateCrate([FromBody] CreateCrateRequest request)
//     {
//         var user = await GetCurrentUserAsync();
//         if (user == null) return Unauthorized();
//
//         var result = await _crateService.CreateCrateAsync(user.Id, request.Name);
//         return result.Succeeded ? Ok(result.Data) : BadRequest(result.Errors);
//     }
//
//     [HttpGet]
//     public async Task<IActionResult> GetUserCrates()
//     {
//         var user = await GetCurrentUserAsync();
//         if (user == null) return Unauthorized();
//
//         var result = await _crateService.GetAllCratesAsync(user.Id);
//         return result.Succeeded ? Ok(result.Data) : BadRequest(result.Errors);
//     }
//
//     [HttpPut("{crateId}/rename")]
//     public async Task<IActionResult> RenameCrate(Guid crateId, [FromBody] RenameCrateRequest request)
//     {
//         var user = await GetCurrentUserAsync();
//         if (user == null) return Unauthorized();
//
//         request.CrateId = crateId;
//         var result = await _crateService.RenameCrateAsync(user.Id, request.CrateId, request.NewName);
//         return result.Succeeded ? Ok(result.Data) : BadRequest(result.Errors);
//     }
// }