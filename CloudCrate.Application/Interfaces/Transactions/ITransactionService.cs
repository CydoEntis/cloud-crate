using CloudCrate.Application.Models;

namespace CloudCrate.Application.Interfaces.Transactions;

public interface ITransactionService
{
    Task<Result> ExecuteAsync(Func<Task> action);
}