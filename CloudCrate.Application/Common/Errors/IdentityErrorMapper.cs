using CloudCrate.Application.Common.Extensions;

namespace CloudCrate.Application.Common.Errors;
public static class IdentityErrorMapper
{
    public static Error Map(string identityErrorCode, string description)
    {
        return identityErrorCode switch
        {
            "DuplicateUserName" => Errors.User.DuplicateUsername(description),
            "InvalidEmail" => Errors.User.InvalidEmail(description),
            "DuplicateEmail" => Errors.User.DuplicateEmail(description),
            "PasswordTooShort" => Errors.User.PasswordTooShort(description),
            "PasswordRequiresNonAlphanumeric" => Errors.User.PasswordRequiresNonAlphanumeric(description),
            "PasswordRequiresDigit" => Errors.User.PasswordRequiresDigit(description),
            "PasswordRequiresUpper" => Errors.User.PasswordRequiresUpper(description),
            "PasswordRequiresLower" => Errors.User.PasswordRequiresLower(description),
            _ => Errors.Common.InternalServerError.WithMessage(description)
        };
    }
}