using CloudCrate.Application.DTOs.Crate.Request;
using CloudCrate.Application.Errors;
using CloudCrate.Application.Interfaces.Storage;
using CloudCrate.Application.Interfaces.Transactions;
using CloudCrate.Application.Interfaces.User;
using CloudCrate.Application.Models;
using CloudCrate.Domain.Entities;
using CloudCrate.Domain.ValueObjects;
using CloudCrate.Infrastructure.Persistence;
using CloudCrate.Infrastructure.Persistence.Mappers;
using Microsoft.Extensions.Logging;

namespace CloudCrate.Infrastructure.Services.Crates;

public class CrateManagementService
{
    private readonly AppDbContext _context;
    private readonly IUserService _userService;
    private readonly IStorageService _storageService;
    private readonly ITransactionService _transactionService;
    private readonly ILogger<CrateManagementService> _logger;

    public CrateManagementService(AppDbContext context, IUserService userService, IStorageService storageService,
        ITransactionService transactionService,
        ILogger<CrateManagementService> logger
    )

    {
        _context = context;
        _userService = userService;
        _storageService = storageService;
        _logger = logger;
        _transactionService = transactionService;
    }

    public async Task<Result<Guid>> CreateCrateAsync(CreateCrateRequest request)
    {
        if (request.UserId != null)
        {
            var requestedBytes = StorageSize.FromGigabytes(request.AllocatedStorageGb).Bytes;
            var canAllocate = await _userService.CanConsumeStorageAsync(request.UserId, requestedBytes);
            if (!canAllocate.IsSuccess)
            {
                _logger.LogError("User {UserId} cannot allocate {RequestedBytes} bytes: {Error}",
                    request.UserId, requestedBytes, canAllocate.Error?.Message);
                return Result<Guid>.Failure(canAllocate.Error ?? new InternalError("Storage allocation check failed"));
            }
        }

        Crate crate;
        try
        {
            crate = Crate.Create(request.Name, request.UserId, request.AllocatedStorageGb, request.Color);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create crate with name {CrateName}", request.Name);
            return Result<Guid>.Failure(new ValidationError($"Invalid crate creation parameters: {ex.Message}"));
        }

        var bucketResult = await _storageService.EnsureBucketExistsAsync();
        if (!bucketResult.IsSuccess)
        {
            _logger.LogError("Failed to ensure main bucket exists: {Error}", bucketResult.Error?.Message);
            return Result<Guid>.Failure(bucketResult.Error);
        }

        var transactionResult = await _transactionService.ExecuteAsync(async () =>
        {
            var crateEntity = crate.ToEntity(); // 👈 domain → entity
            _context.Crates.Add(crateEntity);
            await _context.SaveChangesAsync();
        });

        if (!transactionResult.IsSuccess)
        {
            _logger.LogError("Failed to save crate {CrateId} in transaction: {Error}",
                crate.Id, transactionResult.Error?.Message);
            await _storageService.DeleteAllFilesForCrateAsync(crate.Id);
            return Result<Guid>.Failure(transactionResult.Error ??
                                        new InternalError("Failed to create crate in database"));
        }

        return Result<Guid>.Success(crate.Id);
    }
}