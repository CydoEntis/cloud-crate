using CloudCrate.Application.Errors;
using CloudCrate.Application.Models;
using Microsoft.AspNetCore.Identity;

namespace CloudCrate.Infrastructure.Identity;

public static class IdentityErrorMapper
{
    public static Error Map(string identityErrorCode, string description)
    {
        return identityErrorCode switch
        {
            "DuplicateUserName" => new ConflictError($"Username is already taken"),
            "DuplicateEmail" => new ConflictError($"Email is already in use"),
            "InvalidEmail" => new ValidationError($"Email is invalid", "Email"),

            "PasswordTooShort" => new ValidationError("Password is too short", "Password"),
            "PasswordRequiresNonAlphanumeric" => new ValidationError("Password must contain a non-alphanumeric character", "Password"),
            "PasswordRequiresDigit" => new ValidationError("Password must contain a digit", "Password"),
            "PasswordRequiresUpper" => new ValidationError("Password must contain an uppercase letter", "Password"),
            "PasswordRequiresLower" => new ValidationError("Password must contain a lowercase letter", "Password"),

            "InvalidToken" => new UnauthorizedError("Invalid token"),
            "DefaultError" => new InternalError(description),
            "ConcurrencyFailure" => new InternalError("Concurrency failure occurred"),

            _ => new InternalError(description) 
        };
    }

    public static Result<T> ToResult<T>(this IdentityResult identityResult, T value)
    {
        if (identityResult.Succeeded)
            return Result<T>.Success(value);

        var errors = identityResult.Errors
            .Select(e => Map(e.Code, e.Description))
            .ToArray();

        return errors.Length == 1
            ? Result<T>.Failure(errors[0])
            : Result<T>.Failure(Error.Validations(errors.OfType<ValidationError>()));
    }

    public static Result ToResult(this IdentityResult identityResult)
    {
        if (identityResult.Succeeded)
            return Result.Success();

        var errors = identityResult.Errors
            .Select(e => Map(e.Code, e.Description))
            .ToArray();

        return errors.Length == 1
            ? Result.Failure(errors[0])
            : Result.Failure(Error.Validations(errors.OfType<ValidationError>()));
    }
}
