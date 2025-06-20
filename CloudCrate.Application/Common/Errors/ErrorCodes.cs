namespace CloudCrate.Application.Common.Errors;

public record Error(string Code, string Message);

public static class Errors
{
    public static readonly Error CrateLimitReached =
        new("ERR_CRATE_LIMIT", "Crate limit reached for your subscription plan.");

    public static readonly Error CrateNotFound = new("ERR_CRATE_NOT_FOUND", "Crate not found or access denied.");

    public static readonly Error Unauthorized =
        new("ERR_UNAUTHORIZED", "You are not authorized to perform this action.");

    public static readonly Error ValidationFailed = new("ERR_VALIDATION_FAILED", "Validation failed for the request.");
    public static readonly Error InternalServerError = new("ERR_INTERNAL", "An internal error has occurred.");
    public static readonly Error UserNotFound = new("ERR_USER_NOT_FOUND", "User not found.");
}