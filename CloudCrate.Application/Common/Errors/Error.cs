namespace CloudCrate.Application.Common.Errors;

public readonly record struct Error(string Code, string Message, int StatusCode = 400);

public static class Errors
{
    public static class Crates
    {
        public static readonly Error LimitReached =
            new("ERR_CRATE_LIMIT", "Crate limit reached for your subscription plan.", 403);

        public static readonly Error NotFound =
            new("ERR_CRATE_NOT_FOUND", "Crate not found or access denied.", 404);
    }

    public static class Folders
    {
        public static readonly Error NotFound =
            new("ERR_FOLDER_NOT_FOUND", "Folder not found or access denied.", 404);

        public static readonly Error CreationFailed =
            new("ERR_FOLDER_CREATION_FAILED", "Failed to create folder for storing the file.", 500);

        public static readonly Error NotEmpty =
            new("ERR_FOLDER_NOT_EMPTY", "Cannot delete a folder that contains subfolders or files.", 409);

        public static readonly Error InvalidMove =
            new("ERR_INVALID_MOVE", "Cannot move a folder into itself or one of its own descendants.", 400);
    }

    public static class Files
    {
        public static readonly Error NotFound =
            new("ERR_FILE_NOT_FOUND", "File not found or access denied.", 404);

        public static readonly Error SaveFailed =
            new("ERR_FILE_SAVE_FAILED", "Failed to save the file to disk.", 500);

        public static readonly Error ReadFailed =
            new("ERR_FILE_READ_FAILED", "Failed to read file from disk.", 500);

        public static readonly Error DeleteFailed =
            new("ERR_FILE_DELETE_FAILED", "Failed to delete file from disk.", 500);

        public static readonly Error AlreadyExists =
            new("ERR_FILE_EXISTS", "A file with the same name already exists.", 409);

        public static Error AccessFailed =>
            new Error("Files.AccessFailed", "Failed to generate access URL for the file.", 403);

        public static readonly Error FileTooLarge =
            new("ERR_FILE_TOO_LARGE", "The uploaded file exceeds the maximum allowed size of 10MB.", 413);

        public static readonly Error VideoNotAllowed =
            new("ERR_FILE_VIDEO_NOT_ALLOWED", "Video files are not allowed.", 415);
    }

    public static class User
    {
        public static readonly Error NotFound =
            new("ERR_USER_NOT_FOUND", "User not found.", 404);

        public static readonly Error Unauthorized =
            new("ERR_UNAUTHORIZED", "You are not authorized to perform this action.", 401);

        public static readonly Error OwnerRoleRemovalNotAllowed =
            new("ERR_OWNER_ROLE_REMOVAL_NOT_ALLOWED", "Owners cannot remove their own owner role.", 400);

        public static Error DuplicateUsername(string? message = null) =>
            new("ERR_DUPLICATE_USERNAME", message ?? "A user with this username already exists.", 409);

        public static Error InvalidEmail(string? message = null) =>
            new("ERR_INVALID_EMAIL", message ?? "The provided email address is invalid.", 400);

        public static Error DuplicateEmail(string? message = null) =>
            new("ERR_DUPLICATE_EMAIL", message ?? "A user with this email already exists.", 409);

        public static Error PasswordTooShort(string? message = null) =>
            new("ERR_PASSWORD_TOO_SHORT", message ?? "Password is too short.", 400);

        public static Error PasswordRequiresNonAlphanumeric(string? message = null) =>
            new("ERR_PASSWORD_REQUIRES_NON_ALPHANUMERIC", message ?? "Password must contain a symbol.", 400);

        public static Error PasswordRequiresDigit(string? message = null) =>
            new("ERR_PASSWORD_REQUIRES_DIGIT", message ?? "Password must contain a digit.", 400);

        public static Error PasswordRequiresUpper(string? message = null) =>
            new("ERR_PASSWORD_REQUIRES_UPPER", message ?? "Password must contain an uppercase letter.", 400);

        public static Error PasswordRequiresLower(string? message = null) =>
            new("ERR_PASSWORD_REQUIRES_LOWER", message ?? "Password must contain a lowercase letter.", 400);
    }

    public static class Validation
    {
        public static readonly Error Failed =
            new("ERR_VALIDATION_FAILED", "Validation failed for the request.", 400);
    }

    public static class Storage
    {
        public static readonly Error Failure =
            new("ERR_STORAGE_FAILED", "A storage error occurred.", 500);
    }

    public static class Invites
    {
        public static readonly Error NotFound =
            new("ERR_INVITE_NOT_FOUND", "Invite token not found or invalid.", 404);

        public static readonly Error Invalid =
            new("ERR_INVITE_INVALID", "Invite has already been used or declined.", 400);

        public static readonly Error Expired =
            new("ERR_INVITE_EXPIRED", "This invite link has expired.", 410);

        public static readonly Error AlreadyExists =
            new("ERR_INVITE_EXISTS", "An invite has already been sent to this email for this crate.", 409);
    }

    public static class Roles
    {
        public static Error Validation(string code, string message) =>
            new(code, message, 400);
    }

    public static class Email
    {
        public static readonly Error SendFailed =
            new("ERR_EMAIL_SEND_FAILED", "Failed to send the email.", 500);

        public static Error SendException(string details) =>
            new("ERR_EMAIL_SEND_EXCEPTION", $"Error sending email: {details}", 500);
    }

    public static class Common
    {
        public static readonly Error InternalServerError =
            new("ERR_INTERNAL", "An internal error has occurred.", 500);

        public static readonly Error Unexpected =
            new("ERR_UNEXPECTED", "An unexpected error occurred.", 500);

        public static readonly Error Unknown =
            new("ERR_UNKNOWN", "An unknown error occurred.", 500);
    }

    // Helper methods for dynamic/custom errors
    public static Error Custom(string code, string message, int statusCode = 400) =>
        new(code, message, statusCode);

    public static Error FromException(Exception ex) =>
        new("ERR_EXCEPTION", ex.Message, 500);
}