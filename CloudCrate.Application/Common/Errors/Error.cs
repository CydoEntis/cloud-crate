namespace CloudCrate.Application.Common.Errors;

public abstract record Error(string Message)
{
    public static ValidationError Validation(string message, string? field = null) => new(message, field);

    public static ValidationErrors Validations(params ValidationError[] errors) => new(errors);
    public static ValidationErrors Validations(IEnumerable<ValidationError> errors) => new(errors.ToArray());

    public static NotFoundError NotFound(string? message = null, string? resourceType = null, string? resourceId = null)
        => new(message ?? "Resource not found", resourceType, resourceId);

    public static ConflictError Conflict(string? message = null, string? conflictingField = null)
        => new(message ?? "Conflict occurred", conflictingField);

    public static UnauthorizedError Unauthorized(string? message = null) => new(message ?? "Access denied");

    public static ForbiddenError Forbidden(string? message = null) => new(message ?? "Insufficient permissions");

    public static InternalError Internal(string? message = null) => new(message ?? "An internal error occurred");

    public static BusinessRuleError BusinessRule(string? message = null, string? ruleCode = null)
        => new(message ?? "Business rule violation", ruleCode);
}

public sealed record ValidationError(string Message, string? Field = null) : Error(Message);

public sealed record ValidationErrors(ValidationError[] Errors) : Error("Multiple validation errors occurred")
{
    public IReadOnlyList<ValidationError> ErrorList => Errors;

    public bool HasFieldError(string fieldName) =>
        Errors.Any(e => string.Equals(e.Field, fieldName, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<ValidationError> GetFieldErrors(string fieldName) =>
        Errors.Where(e => string.Equals(e.Field, fieldName, StringComparison.OrdinalIgnoreCase));
}

public sealed record NotFoundError(string Message, string? ResourceType = null, string? ResourceId = null)
    : Error(Message);

public sealed record ConflictError(string Message, string? ConflictingField = null) : Error(Message);

public sealed record UnauthorizedError(string Message = "Access denied") : Error(Message);

public sealed record ForbiddenError(string Message = "Insufficient permissions") : Error(Message);

public sealed record InternalError(string Message = "An internal error occurred") : Error(Message);

public sealed record BusinessRuleError(string Message = "Business rule violation", string? RuleCode = null) : Error(Message);

public sealed record FileError(string Message = "File error", string? FileName = null) : Error(Message);

public sealed record AlreadyExistsError(string Message = "Resource already exists") : Error(Message);

public sealed record EmailSendError(string Message = "Failed to send email") : Error(Message);

public sealed record StorageError(string Message = "A storage operation failed") : Error(Message);

public sealed record FileAccessError(string Message = "Failed to access the file") : Error(Message);

public sealed record FileSaveError(string Message = "Failed to save the file") : Error(Message);

public sealed record FileReadError(string Message = "Failed to read the file") : Error(Message);

public sealed record FileDeleteError(string Message = "Failed to delete the file") : Error(Message);

public sealed record FileNotFoundError(string Message = "File not found") : Error(Message);
