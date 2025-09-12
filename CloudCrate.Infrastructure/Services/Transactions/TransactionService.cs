using CloudCrate.Application.Errors;
using CloudCrate.Application.Interfaces.Transactions;
using CloudCrate.Application.Models;
using CloudCrate.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace CloudCrate.Infrastructure.Services.Transactions;

public class TransactionService : ITransactionService
{
    private readonly AppDbContext _context;
    private readonly ILogger<TransactionService> _logger;

    public TransactionService(AppDbContext context, ILogger<TransactionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result> ExecuteAsync(Func<Task> action)
    {
        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            await action();
            await tx.CommitAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Transaction failed");
            return Result.Failure(new InternalError(ex.Message));
        }
    }
}