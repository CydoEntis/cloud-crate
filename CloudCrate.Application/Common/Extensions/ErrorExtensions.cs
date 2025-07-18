using CloudCrate.Application.Common.Errors;

namespace CloudCrate.Application.Common.Extensions;

public static class ErrorExtensions
{
    public static Error WithMessage(this Error error, string message)
    {
        return new Error(error.Code, message, error.StatusCode);
    }
}