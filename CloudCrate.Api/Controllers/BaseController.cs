using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace CloudCrate.Api.Controllers;

[ApiController]
public abstract class BaseController : ControllerBase
{
    protected string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
}