namespace CloudCrate.Application.Common.Errors;

public record Error(string Code, string Message);

public static class Errors
{
    public static readonly Error CrateLimitReached =
        new("ERR_CRATE_LIMIT", "Crate limit reached for your subscription plan.");

    public static readonly Error CrateNotFound =
        new("ERR_CRATE_NOT_FOUND", "Crate not found or access denied.");

    public static readonly Error FolderNotFound =
        new("ERR_FOLDER_NOT_FOUND", "Folder not found or access denied.");

    public static readonly Error FileNotFound =
        new("ERR_FILE_NOT_FOUND", "File not found or access denied.");

    public static readonly Error Unauthorized =
        new("ERR_UNAUTHORIZED", "You are not authorized to perform this action.");

    public static readonly Error ValidationFailed =
        new("ERR_VALIDATION_FAILED", "Validation failed for the request.");

    public static readonly Error InternalServerError =
        new("ERR_INTERNAL", "An internal error has occurred.");

    public static readonly Error UserNotFound =
        new("ERR_USER_NOT_FOUND", "User not found.");

    public static readonly Error StorageFailure =
        new("ERR_STORAGE_FAILED", "A storage error occurred.");

    public static readonly Error FolderCreationFailed =
        new("ERR_FOLDER_CREATION_FAILED", "Failed to create folder for storing the file.");

    public static readonly Error FileSaveFailed =
        new("ERR_FILE_SAVE_FAILED", "Failed to save the file to disk.");

    public static readonly Error FileReadFailed =
        new("ERR_FILE_READ_FAILED", "Failed to read file from disk.");

    public static readonly Error FileDeleteFailed =
        new("ERR_FILE_DELETE_FAILED", "Failed to delete file from disk.");

    public static readonly Error FileAlreadyExists =
        new("ERR_FILE_EXISTS", "A file with the same name already exists.");

    public static readonly Error FolderNotEmpty =
        new("ERR_FOLDER_NOT_EMPTY", "Cannot delete a folder that contains subfolders or files.");

    public static readonly Error InvalidMove =
        new("ERR_INVALID_MOVE", "Cannot move a folder into itself or one of its own descendants.");

    public static readonly Error InviteNotFound =
        new("ERR_INVITE_NOT_FOUND", "Invite token not found or invalid.");

    public static readonly Error InviteInvalid =
        new("ERR_INVITE_INVALID", "Invite has already been used or declined.");

    public static readonly Error EmailSendFailed =
        new("ERR_EMAIL_SEND_FAILED", "Failed to send the email.");

    public static Error EmailSendException(string details) =>
        new("ERR_EMAIL_SEND_EXCEPTION", $"Error sending email: {details}");

    public static readonly Error OwnerRoleRemovalNotAllowed =
        new("ERR_OWNER_ROLE_REMOVAL_NOT_ALLOWED", "Owners cannot remove their own owner role.");
    
    public static Error Unexpected(string message) =>
        new("ERR_UNEXPECTED", message);

    public static Error Custom(string code, string message) =>
        new(code, message);
}