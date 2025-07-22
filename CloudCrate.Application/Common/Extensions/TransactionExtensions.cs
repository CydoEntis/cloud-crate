using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Models;
using Microsoft.EntityFrameworkCore.Storage;

namespace CloudCrate.Application.Common.Extensions;

public static class TransactionExtensions
{
    public static async Task<Result<T>> RollbackWithFailure<T>(this IDbContextTransaction transaction,
        List<Error> errors)
    {
        await transaction.RollbackAsync();
        return Result<T>.Failure(errors);
    }
}