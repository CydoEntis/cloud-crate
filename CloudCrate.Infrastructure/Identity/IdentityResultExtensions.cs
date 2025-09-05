using CloudCrate.Application.Errors;
using CloudCrate.Application.Models;
using Microsoft.AspNetCore.Identity;

namespace CloudCrate.Application.Common.Extensions;

public static class IdentityResultExtensions
{
    public static Result ToResult(this IdentityResult identityResult)
    {
        if (identityResult.Succeeded)
            return Result.Success();

        var errors = identityResult.Errors
            .Select(e => Error.Validation(e.Description, MapField(e.Code)))
            .ToArray();

        return Error.Validations(errors);
    }

    public static Result<T> ToResult<T>(this IdentityResult identityResult, T value)
    {
        if (identityResult.Succeeded)
            return Result<T>.Success(value);

        var errors = identityResult.Errors
            .Select(e => Error.Validation(e.Description, MapField(e.Code)))
            .ToArray();

        return Error.Validations(errors);
    }

    private static string? MapField(string code) => code switch
    {
        "DuplicateUserName" or "InvalidUserName" or "DuplicateEmail" or "InvalidEmail"
            => "Email",

        "PasswordTooShort" or "PasswordRequiresNonAlphanumeric" or "PasswordRequiresDigit"
            or "PasswordRequiresLower" or "PasswordRequiresUpper"
            => "Password",

        _ => null 
    };
}