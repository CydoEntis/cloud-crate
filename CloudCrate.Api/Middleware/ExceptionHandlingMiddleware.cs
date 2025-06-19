using CloudCrate.Api.Models;

namespace CloudCrate.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred");

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            var errorMessage = _env.IsDevelopment() ? ex.Message : "An unexpected error occurred.";
            var errors = new Dictionary<string, string>
            {
                { "general", errorMessage }
            };

            var response = ApiResponse<string>.FailResponse(errors);

            await context.Response.WriteAsJsonAsync(response);
        }
    }
}