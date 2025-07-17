using System.Collections.Generic;

namespace CloudCrate.Api.Models
{
    public static class ErrorCodeHttpStatusMapper
    {
        private static readonly Dictionary<string, int> _errorStatusMap = new()
        {
            // Crate
            ["ERR_CRATE_LIMIT"] = 403,
            ["ERR_CRATE_NOT_FOUND"] = 404,
            
            // Folder
            ["ERR_FOLDER_NOT_FOUND"] = 404,
            ["ERR_FOLDER_CREATION_FAILED"] = 500,
            ["ERR_FOLDER_NOT_EMPTY"] = 409,

            //File
            ["ERR_FILE_SAVE_FAILED"] = 500,
            ["ERR_FILE_READ_FAILED"] = 500,
            ["ERR_FILE_DELETE_FAILED"] = 500,
            ["ERR_FILE_EXISTS"] = 409,
            ["ERR_FILE_NOT_FOUND"] = 404,

            // User
            ["ERR_USER_NOT_FOUND"] = 404,
            
            //Storage
            ["ERR_STORAGE_FAILED"] = 500,

            // Move (File and Folder)
            ["ERR_INVALID_MOVE"] = 400,
            
            // Invites
            ["ERR_INVITE_NOT_FOUND"] = 404,
            ["ERR_INVITE_INVALID"] = 400,
            
            // Email
            ["ERR_EMAIL_SEND_FAILED"] = 500,
            ["ERR_EMAIL_SEND_EXCEPTION"] = 500,
            
            // Roles
            ["ERR_OWNER_ROLE_REMOVAL_NOT_ALLOWED"] = 400,
            
            // Generic
            ["ERR_UNAUTHORIZED"] = 401,
            ["ERR_VALIDATION_FAILED"] = 400,
            ["ERR_INTERNAL"] = 500,
            ["ERR_UNEXPECTED"] = 500,

        };

        public static int GetHttpStatusCode(string errorCode)
        {
            return _errorStatusMap.TryGetValue(errorCode, out var statusCode)
                ? statusCode
                : 400;
        }
    }
}