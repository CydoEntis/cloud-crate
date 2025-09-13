using CloudCrate.Application.Common.Utils;
using CloudCrate.Application.DTOs.Crate.Request;
using CloudCrate.Application.DTOs.Crate.Response;
using CloudCrate.Application.DTOs.Pagination;
using CloudCrate.Application.DTOs.User.Response;
using CloudCrate.Application.Errors;
using CloudCrate.Application.Extensions;
using CloudCrate.Application.Interfaces;
using CloudCrate.Application.Interfaces.Crate;
using CloudCrate.Application.Interfaces.Storage;
using CloudCrate.Application.Interfaces.User;
using CloudCrate.Application.Interfaces.Permissions;
using CloudCrate.Application.Mappers;
using CloudCrate.Application.Models;
using CloudCrate.Domain.Entities;
using CloudCrate.Domain.Enums;
using CloudCrate.Infrastructure.Persistence;
using CloudCrate.Infrastructure.Persistence.Entities;
using CloudCrate.Infrastructure.Persistence.Mappers;
using CloudCrate.Infrastructure.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;


namespace CloudCrate.Infrastructure.Services.Crates;

public class CrateService : ICrateService
{
    private readonly AppDbContext _context;
    private readonly IUserService _userService;
    private readonly IStorageService _storageService;
    private readonly ICrateRoleService _crateRoleService;
    private readonly IBatchDeleteService _batchDeleteService;
    private readonly ILogger<CrateService> _logger;


    public CrateService(
        AppDbContext context,
        IUserService userService,
        IStorageService storageService,
        ICrateRoleService crateRoleService,
        IBatchDeleteService batchDeleteService,
        ILogger<CrateService> logger)
    {
        _context = context;
        _userService = userService;
        _storageService = storageService;
        _crateRoleService = crateRoleService;
        _batchDeleteService = batchDeleteService;
        _logger = logger;
    }

    public async Task<Result<Guid>> CreateCrateAsync(CreateCrateRequest request)
    {
        var validationResult = await _userService.CanAllocateStorageAsync(
            request.UserId, request.AllocatedStorageGb);
        if (validationResult.IsFailure) return Result<Guid>.Failure(validationResult.Error!);

        var bucketResult = await _storageService.EnsureBucketExistsAsync();
        if (bucketResult.IsFailure) return Result<Guid>.Failure(bucketResult.Error!);

        try
        {
            var crate = Crate.Create(request.Name, request.UserId, request.AllocatedStorageGb, request.Color);
            return await CreateCrateOrRollbackAsync(crate, request.UserId);
        }
        catch (Exception ex)
        {
            return Result<Guid>.Failure(new ValidationError(ex.Message));
        }
    }

    private async Task<Result<Guid>> CreateCrateOrRollbackAsync(Crate crate, string userId)
    {
        var crateEntity = crate.ToEntity();
        var requestedBytes = crate.AllocatedStorage.Bytes;

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var allocationResult = await _userService.AllocateStorageAsync(userId, requestedBytes);
            if (!allocationResult.IsSuccess)
            {
                await transaction.RollbackAsync();
                return Result<Guid>.Failure(allocationResult.Error!);
            }

            _context.Crates.Add(crateEntity);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            _logger.LogInformation("Successfully created crate {CrateId} for user {UserId}", crate.Id, userId);
            return Result<Guid>.Success(crate.Id);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to create crate {CrateId}: {Error}", crate.Id, ex.Message);

            await _storageService.DeleteAllFilesForCrateAsync(crate.Id);

            return Result<Guid>.Failure(new InternalError($"Failed to create crate: {ex.Message}"));
        }
    }

    public async Task<Result> UpdateCrateAsync(Guid crateId, string userId, UpdateCrateRequest request)
    {
        var canManageResult = await _crateRoleService.CanManageCrate(crateId, userId);
        if (!canManageResult.IsSuccess || !canManageResult.Value)
            return Result.Failure(new ForbiddenError("User cannot update this crate"));

        var crateEntity = await _context.Crates.FirstOrDefaultAsync(c => c.Id == crateId);
        if (crateEntity is null)
            return Result.Failure(new NotFoundError("Crate not found"));

        var crateDomain = crateEntity.ToDomain();

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            crateDomain.Rename(request.Name);
            crateDomain.SetColor(request.Color);

            var currentAllocationGB = (int)Math.Round(crateDomain.AllocatedStorage.ToGigabytes());
            if (request.StorageAllocationGb != currentAllocationGB)
            {
                var userAllocationResult = await _userService.ReallocateStorageAsync(
                    userId, currentAllocationGB, request.StorageAllocationGb);
                if (!userAllocationResult.IsSuccess)
                {
                    await transaction.RollbackAsync();
                    return Result.Failure(userAllocationResult.Error!);
                }

                if (!crateDomain.TryAllocateStorage(request.StorageAllocationGb, out var error))
                {
                    await transaction.RollbackAsync();
                    return Result.Failure(new StorageError(error ?? "Failed to allocate storage"));
                }
            }

            crateEntity.UpdateEntity(crateDomain);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Result.Success();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to update crate {CrateId}", crateDomain.Id);
            return Result.Failure(new InternalError($"Failed to update crate: {ex.Message}"));
        }
    }

    public async Task<Result<PaginatedResult<CrateListItemResponse>>> GetCratesAsync(CrateQueryParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.UserId))
            return Result<PaginatedResult<CrateListItemResponse>>.Failure(
                new UnauthorizedError("User must be logged in"));

        try
        {
            var query = BuildUserCratesQuery(parameters);
            var pagedEntities = await query.PaginateAsync(parameters.Page, parameters.PageSize);

            var responses = pagedEntities.Items
                .Select(e => e.ToDomain().ToCrateListItemResponse(parameters.UserId))
                .ToList();

            return Result<PaginatedResult<CrateListItemResponse>>.Success(
                PaginatedResult<CrateListItemResponse>.Create(responses, pagedEntities.TotalCount, parameters.Page,
                    parameters.PageSize));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve crates for user {UserId}", parameters.UserId);
            return Result<PaginatedResult<CrateListItemResponse>>.Failure(new InternalError("Could not fetch crates"));
        }
    }

    private IQueryable<CrateEntity> BuildUserCratesQuery(CrateQueryParameters parameters)
    {
        var query = _context.Crates
            .AsNoTracking()
            .Include(c => c.Members).ThenInclude(m => m.User)
            .Where(c => c.Members.Any(m => m.UserId == parameters.UserId));

        query = parameters.MemberType switch
        {
            CrateMemberType.Owner => query.Where(c =>
                c.Members.Any(m => m.UserId == parameters.UserId && m.Role == CrateRole.Owner)),
            CrateMemberType.Joined => query.Where(c =>
                c.Members.Any(m => m.UserId == parameters.UserId && m.Role != CrateRole.Owner)),
            _ => query
        };

        return query.ApplySearch(parameters.SearchTerm).ApplyOrdering(parameters);
    }


    public async Task<Result<CrateDetailsResponse>> GetCrateAsync(Guid crateId, string userId)
    {
        var canViewResult = await _crateRoleService.CanView(crateId, userId);
        if (!canViewResult.IsSuccess)
            return Result<CrateDetailsResponse>.Failure(canViewResult.Error);

        var crateEntity = await _context.Crates.AsNoTracking()
            .Include(c => c.Members)
            .Include(c => c.Files)
            .Include(c => c.Folders)
            .FirstOrDefaultAsync(c => c.Id == crateId);

        if (crateEntity is null)
            return Result<CrateDetailsResponse>.Failure(new NotFoundError("Crate not found"));

        var crateDomain = crateEntity.ToDomain();

        // Since CanView already verified membership, this should never be null
        var member = crateDomain.Members.First(m => m.UserId == userId);

        var rootFolder = crateDomain.Folders.FirstOrDefault(f => f.ParentFolderId == null);
        if (rootFolder is null)
            return Result<CrateDetailsResponse>.Failure(new InternalError("Root folder missing"));

        var response = new CrateDetailsResponse
        {
            Id = crateDomain.Id,
            Name = crateDomain.Name,
            Color = crateDomain.Color,
            Role = member.Role,
            TotalUsedStorage = crateDomain.UsedStorage.Bytes,
            StorageLimit = crateDomain.AllocatedStorage.Bytes,
            BreakdownByType = FileBreakdownHelper.GetFilesByMimeTypeInMemory(crateDomain.Files),
            RootFolderId = rootFolder.Id
        };

        return Result<CrateDetailsResponse>.Success(response);
    }


    public async Task<Result> DeleteCrateAsync(Guid crateId, string userId)
    {
        var canManageResult = await _crateRoleService.CanManageCrate(crateId, userId);
        if (!canManageResult.IsSuccess || !canManageResult.Value)
            return Result.Failure(new ForbiddenError("You do not have permission to delete this crate"));

        var crateEntity = await _context.Crates.FirstOrDefaultAsync(c => c.Id == crateId);
        if (crateEntity is null)
            return Result.Failure(new NotFoundError("Crate not found"));

        var crateDomain = crateEntity.ToDomain();

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var deallocationResult =
                await _userService.DeallocateStorageAsync(userId, crateDomain.AllocatedStorage.Bytes);
            if (!deallocationResult.IsSuccess)
            {
                await transaction.RollbackAsync();
                return Result.Failure(deallocationResult.Error!);
            }

            var deleteResult = await _batchDeleteService.DeleteCratesAsync(new[] { crateDomain.Id });
            if (!deleteResult.IsSuccess)
            {
                await transaction.RollbackAsync();
                return Result.Failure(deleteResult.Error!);
            }

            await transaction.CommitAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to delete crate {CrateId}", crateId);
            return Result.Failure(new InternalError($"Failed to delete crate: {ex.Message}"));
        }
    }


    public async Task<Result> DeleteCratesAsync(IEnumerable<Guid> crateIds, string userId)
    {
        var crateIdList = crateIds.ToList();

        var crateEntities = await _context.Crates
            .Include(c => c.Members)
            .Where(c => crateIdList.Contains(c.Id))
            .ToListAsync();

        if (!crateEntities.Any())
            return Result.Failure(new NotFoundError("No crates found"));

        var ownedCrates = crateEntities
            .Where(c => c.Members.Any(m => m.UserId == userId && m.Role == CrateRole.Owner))
            .ToList();

        if (!ownedCrates.Any())
            return Result.Failure(new ForbiddenError("You do not have permission to delete these crates"));

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var totalStorageToFree = ownedCrates.Sum(c => c.AllocatedStorageBytes);
            var deallocationResult = await _userService.DeallocateStorageAsync(userId, totalStorageToFree);
            if (!deallocationResult.IsSuccess)
            {
                await transaction.RollbackAsync();
                return Result.Failure(deallocationResult.Error!);
            }

            var deleteResult = await _batchDeleteService.DeleteCratesAsync(ownedCrates.Select(c => c.Id));
            if (!deleteResult.IsSuccess)
            {
                await transaction.RollbackAsync();
                return Result.Failure(deleteResult.Error!);
            }

            await transaction.CommitAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to delete crates: {CrateIds}",
                string.Join(", ", ownedCrates.Select(c => c.Id)));
            return Result.Failure(new InternalError("Failed to delete crates"));
        }
    }
}