using CloudCrate.Api.Models;
using CloudCrate.Application.Common.Errors;

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

            if (context.Response.HasStarted)
            {
                _logger.LogWarning("Response already started, cannot write error response.");
                throw;
            }

            context.Response.Clear();
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            var errorMessage = _env.IsDevelopment()
                ? $"{ex.Message} — {ex.GetType().Name}"
                : "An unexpected error occurred.";

            var error = new Error("ERR_UNHANDLED_EXCEPTION", errorMessage);

            var response = new ApiResponse<object>(
                isSuccess: false,
                value: null,
                message: "Unhandled exception",
                statusCode: StatusCodes.Status500InternalServerError,
                errors: new List<Error> { error }
            );

            await context.Response.WriteAsJsonAsync(response);
        }
    }
}