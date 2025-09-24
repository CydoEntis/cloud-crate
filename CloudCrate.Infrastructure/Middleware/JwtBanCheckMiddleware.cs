using CloudCrate.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace CloudCrate.Infrastructure.Middleware;

public class JwtBanCheckMiddleware
{
    private readonly RequestDelegate _next;

    public JwtBanCheckMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> userManager)
    {
        if (IsAuthEndpoint(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var token = GetTokenFromHeader(context.Request.Headers["Authorization"]);
        
        if (token != null)
        {
            var userId = GetUserIdFromToken(token);
            
            if (userId != null)
            {
                var user = await userManager.FindByIdAsync(userId);
                
                if (user != null && await userManager.IsLockedOutAsync(user))
                {
                    context.Response.StatusCode = 401;
                    context.Response.ContentType = "application/json";
                    
                    var response = new
                    {
                        isSuccess = false,
                        message = "Account has been suspended",
                        statusCode = 401
                    };
                    
                    await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
                    return;
                }
            }
        }
        
        await _next(context);
    }

    private static bool IsAuthEndpoint(PathString path)
    {
        var authPaths = new[] { "/api/auth/login", "/api/auth/register", "/api/auth/refresh", "/api/auth/forgot-password" };
        return authPaths.Any(authPath => path.StartsWithSegments(authPath));
    }

    private static string? GetTokenFromHeader(Microsoft.Extensions.Primitives.StringValues authHeader)
    {
        var header = authHeader.FirstOrDefault();
        if (string.IsNullOrEmpty(header) || !header.StartsWith("Bearer "))
            return null;
            
        return header.Substring("Bearer ".Length).Trim();
    }

    private static string? GetUserIdFromToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jsonToken = tokenHandler.ReadJwtToken(token);
            
            var userIdClaim = jsonToken?.Claims?.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");
            return userIdClaim?.Value;
        }
        catch
        {
            return null; 
        }
    }
}