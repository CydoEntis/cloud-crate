using CloudCrate.Application.DTOs.Crate.Request;
using CloudCrate.Application.DTOs.Crate.Response;
using CloudCrate.Application.DTOs.Pagination;
using CloudCrate.Application.Errors;
using CloudCrate.Application.Extensions;
using CloudCrate.Application.Interfaces;
using CloudCrate.Application.Interfaces.Crate;
using CloudCrate.Application.Interfaces.File;
using CloudCrate.Application.Interfaces.Persistence;
using CloudCrate.Application.Interfaces.Storage;
using CloudCrate.Application.Interfaces.User;
using CloudCrate.Application.Interfaces.Permissions;
using CloudCrate.Application.Interfaces.Transactions;
using CloudCrate.Application.Mappers;
using CloudCrate.Application.Models;
using CloudCrate.Application.Queries;
using CloudCrate.Domain.Entities;
using CloudCrate.Domain.Enums;
using CloudCrate.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CloudCrate.Infrastructure.Services.Crates;

public class CrateService : ICrateService
{
    private readonly IAppDbContext _context;
    private readonly IUserService _userService;
    private readonly IStorageService _storageService;
    private readonly IFileService _fileService;
    private readonly ICrateRoleService _crateRoleService;
    private readonly ITransactionService _transactionService;
    private readonly IBatchDeleteService _batchDeleteService;
    private readonly ILogger<CrateService> _logger;

    public CrateService(
        IAppDbContext context,
        IUserService userService,
        IStorageService storageService,
        IFileService fileService,
        ICrateRoleService crateRoleService,
        ITransactionService transactionService,
        IBatchDeleteService batchDeleteService,
        ILogger<CrateService> logger)
    {
        _context = context;
        _userService = userService;
        _storageService = storageService;
        _fileService = fileService;
        _crateRoleService = crateRoleService;
        _transactionService = transactionService;
        _batchDeleteService = batchDeleteService;
        _logger = logger;
    }


    public async Task<Result<Guid>> CreateCrateAsync(CreateCrateRequest request)
    {
        var requestedBytes = StorageSize.FromGigabytes(request.AllocatedStorageGb).Bytes;
        var canAllocate = await _userService.CanConsumeStorageAsync(request.UserId, requestedBytes);

        if (!canAllocate.IsSuccess)
        {
            _logger.LogError("User {UserId} cannot allocate {RequestedBytes} bytes: {Error}",
                request.UserId, requestedBytes, canAllocate.Error?.Message);
            return Result<Guid>.Failure(canAllocate.Error ?? new InternalError("Storage allocation check failed"));
        }

        Crate crate;
        try
        {
            crate = Crate.Create(request.Name, request.UserId, request.AllocatedStorageGb, request.Color);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create crate for user {UserId} with name {CrateName}",
                request.UserId, request.Name);
            return Result<Guid>.Failure(new ValidationError($"Invalid crate creation parameters: {ex.Message}"));
        }

        var bucketResult = await _storageService.GetOrCreateBucketAsync(crate.GetCrateStorageName());
        if (!bucketResult.IsSuccess)
        {
            _logger.LogError("Failed to get or create bucket for crate {CrateId}: {Error}",
                crate.Id, bucketResult.Error?.Message);

            // Rollback crate creation
            await DeleteCratesAsync(new[] { crate.Id });

            return Result<Guid>.Failure(bucketResult.Error);
        }

        var transactionResult = await _transactionService.ExecuteAsync(async () =>
        {
            _context.Crates.Add(crate);
            await _context.SaveChangesAsync();
        });

        if (!transactionResult.IsSuccess)
        {
            _logger.LogError("Failed to save crate {CrateId} in transaction: {Error}",
                crate.Id, transactionResult.Error?.Message);

            await DeleteCratesAsync(new[] { crate.Id });

            return Result<Guid>.Failure(transactionResult.Error ??
                                        new InternalError("Failed to create crate in database"));
        }

        return Result<Guid>.Success(crate.Id);
    }

    public async Task<Result<bool>> AllocateCrateStorageAsync(string userId, Guid crateId, int requestedAllocationGB)
    {
        if (requestedAllocationGB < 0)
            return Result<bool>.Failure(new ValidationError("Requested allocation must be >= 0"));

        var canManageResult = await _crateRoleService.CanManageCrate(crateId, userId);
        if (!canManageResult.IsSuccess || !canManageResult.Value)
            return Result<bool>.Failure(new ForbiddenError("User does not have required permissions"));

        var crate = await _context.Crates.FirstOrDefaultAsync(c => c.Id == crateId);
        if (crate == null)
            return Result<bool>.Failure(new NotFoundError("Crate not found"));

        var requestedStorage = StorageSize.FromGigabytes(requestedAllocationGB);
        var canAllocate = await _userService.CanConsumeStorageAsync(userId, requestedStorage.Bytes);
        if (!canAllocate.IsSuccess)
            return Result<bool>.Failure(canAllocate.Error ?? new StorageError("Failed to check quota"));

        if (!crate.TryAllocateStorage(requestedAllocationGB, out var error))
            return Result<bool>.Failure(new StorageError(error ?? "Failed to allocate storage"));

        await _context.SaveChangesAsync();
        return Result<bool>.Success(true);
    }


    public async Task<Result<PaginatedResult<CrateListItemResponse>>> GetCratesAsync(CrateQueryParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.UserId))
            return Result<PaginatedResult<CrateListItemResponse>>.Failure(
                new UnauthorizedError("User must be logged in"));

        try
        {
            var query = _context.Crates.AsNoTracking()
                .Include(c => c.Members)
                .Where(c => c.Members.Any(m => m.UserId == parameters.UserId));

            query = parameters.MemberType switch
            {
                CrateMemberType.Owner => query.Where(c =>
                    c.Members.Any(m => m.UserId == parameters.UserId && m.Role == CrateRole.Owner)),
                CrateMemberType.Joined => query.Where(c =>
                    c.Members.Any(m => m.UserId == parameters.UserId && m.Role != CrateRole.Owner)),
                _ => query
            };

            query = query.ApplySearch(parameters.SearchTerm).ApplyOrdering(parameters);

            var pagedCrates = await query.PaginateAsync(parameters.Page, parameters.PageSize);

            var userIds = pagedCrates.Items.SelectMany(c => c.Members.Select(m => m.UserId)).Distinct().ToList();
            var users = await _userService.GetUsersByIdsAsync(userIds);
            var userLookup = users.ToDictionary(u => u.Id, u => u);

            var crateResponses = pagedCrates.Items.Select(c => c.ToCrateListItemResponse(parameters.UserId, userLookup))
                .ToList();

            return Result<PaginatedResult<CrateListItemResponse>>.Success(
                PaginatedResult<CrateListItemResponse>.Create(crateResponses, pagedCrates.TotalCount, parameters.Page,
                    parameters.PageSize)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve crates for user {UserId}", parameters.UserId);
            return Result<PaginatedResult<CrateListItemResponse>>.Failure(new InternalError("Could not fetch crates"));
        }
    }

    public async Task<Result<CrateDetailsResponse>> GetCrateAsync(Guid crateId, string userId)
    {
        var canViewResult = await _crateRoleService.CanView(crateId, userId);
        if (!canViewResult.IsSuccess)
            return Result<CrateDetailsResponse>.Failure(canViewResult.Error);

        var crate = await _context.Crates.AsNoTracking()
            .Include(c => c.Members)
            .Include(c => c.Files)
            .Include(c => c.Folders)
            .FirstOrDefaultAsync(c => c.Id == crateId);

        if (crate == null) return Result<CrateDetailsResponse>.Failure(new NotFoundError("Crate not found"));

        var member = crate.Members.FirstOrDefault(m => m.UserId == userId);
        if (member == null)
            return Result<CrateDetailsResponse>.Failure(new UnauthorizedError("User is not a member of this crate"));

        var rootFolder = crate.Folders.FirstOrDefault(f => f.ParentFolderId == null);
        if (rootFolder == null) return Result<CrateDetailsResponse>.Failure(new InternalError("Root folder missing"));

        var fileBreakdown = _fileService.GetFilesByMimeTypeInMemory(crate.Files);

        return Result<CrateDetailsResponse>.Success(new CrateDetailsResponse
        {
            Id = crate.Id,
            Name = crate.Name,
            Color = crate.Color,
            Role = member.Role,
            TotalUsedStorage = crate.UsedStorage.Bytes,
            StorageLimit = crate.AllocatedStorage.Bytes,
            BreakdownByType = fileBreakdown,
            RootFolderId = rootFolder.Id
        });
    }

    public async Task<Result<List<CrateMemberResponse>>> GetCrateMembersAsync(Guid crateId, CrateMemberRequest request)
    {
        var canViewResult = await _crateRoleService.CanView(crateId, request.UserId);
        if (!canViewResult.IsSuccess || !canViewResult.Value)
            return Result<List<CrateMemberResponse>>.Failure(canViewResult.Error ??
                                                             new ForbiddenError("User cannot view crate members"));

        var crate = await _context.Crates.AsNoTracking().Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == crateId);
        if (crate == null) return Result<List<CrateMemberResponse>>.Failure(new NotFoundError("Crate not found"));

        var memberUserIds = crate.Members.Select(m => m.UserId).Distinct().ToList();
        if (!memberUserIds.Any()) return Result<List<CrateMemberResponse>>.Success(new List<CrateMemberResponse>());

        var users = await _userService.GetUsersByIdsAsync(memberUserIds);
        var userMap = users.ToDictionary(u => u.Id, u => u);

        var responses = crate.Members.Select(m =>
        {
            userMap.TryGetValue(m.UserId, out var user);
            return new CrateMemberResponse
            {
                UserId = m.UserId,
                DisplayName = user?.DisplayName ?? "Unknown",
                Email = user?.Email ?? string.Empty,
                Role = m.Role,
                ProfilePicture = user?.ProfilePictureUrl ?? string.Empty
            };
        }).ToList();

        return Result<List<CrateMemberResponse>>.Success(responses);
    }

    public async Task<Result> LeaveCrateAsync(Guid crateId, string userId)
    {
        var canLeaveResult = await _crateRoleService.CanView(crateId, userId);
        if (!canLeaveResult.IsSuccess || !canLeaveResult.Value)
            return Result.Failure(canLeaveResult.Error ?? new ForbiddenError("User cannot leave this crate"));

        var crate = await _context.Crates.Include(c => c.Members).FirstOrDefaultAsync(c => c.Id == crateId);
        if (crate == null) return Result.Failure(new NotFoundError("Crate not found"));

        var member = crate.Members.FirstOrDefault(m => m.UserId == userId);
        if (member == null) return Result.Failure(new NotFoundError("Membership not found"));
        if (member.Role == CrateRole.Owner) return Result.Failure(new ValidationError("Owner cannot leave the crate"));

        _context.CrateMembers.Remove(member);
        await _context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result<int>> BulkLeaveCratesAsync(IEnumerable<Guid> crateIds, string userId)
    {
        var cratesToLeave = await _context.Crates.Include(c => c.Members)
            .Where(c => crateIds.Contains(c.Id))
            .ToListAsync();

        var leftCount = 0;
        var transactionResult = await _transactionService.ExecuteAsync(async () =>
        {
            foreach (var crate in cratesToLeave)
            {
                var member = crate.Members.FirstOrDefault(m => m.UserId == userId);
                if (member != null && member.Role != CrateRole.Owner)
                {
                    _context.CrateMembers.Remove(member);
                    leftCount++;
                }
            }

            await _context.SaveChangesAsync();
        });

        return transactionResult.IsSuccess
            ? Result<int>.Success(leftCount)
            : Result<int>.Failure(transactionResult.Error);
    }


    public async Task<Result<CrateListItemResponse>> UpdateCrateAsync(Guid crateId, string userId, string? newName,
        string? newColor)
    {
        var canManageResult = await _crateRoleService.CanManageCrate(crateId, userId);
        if (!canManageResult.IsSuccess || !canManageResult.Value)
            return Result<CrateListItemResponse>.Failure(canManageResult.Error ??
                                                         new ForbiddenError("User cannot update this crate"));

        var crate = await _context.Crates.Include(c => c.Members).FirstOrDefaultAsync(c => c.Id == crateId);
        if (crate == null) return Result<CrateListItemResponse>.Failure(new NotFoundError("Crate not found"));

        if (!string.IsNullOrWhiteSpace(newName)) crate.Rename(newName);
        if (!string.IsNullOrWhiteSpace(newColor)) crate.SetColor(newColor);

        await _context.SaveChangesAsync();

        var userIds = crate.Members.Select(m => m.UserId).Distinct().ToList();
        var users = await _userService.GetUsersByIdsAsync(userIds);
        var userLookup = users.ToDictionary(u => u.Id, u => u);

        return Result<CrateListItemResponse>.Success(crate.ToCrateListItemResponse(userId, userLookup));
    }


    public async Task<Result> DeleteCrateAsync(Guid crateId)
    {
        var crate = await _context.Crates.AsNoTracking().FirstOrDefaultAsync(c => c.Id == crateId);
        if (crate == null) return Result.Failure(new NotFoundError("Crate not found"));

        var result = await _batchDeleteService.DeleteCratesAsync(new[] { crate.Id });
        if (!result.IsSuccess)
            _logger.LogWarning("Failed to delete crate {CrateId}", crateId);

        return result;
    }

    public async Task<Result> DeleteCratesAsync(IEnumerable<Guid> crateIds)
    {
        var crates = await _context.Crates.Where(c => crateIds.Contains(c.Id)).Select(c => c.Id).ToListAsync();
        if (!crates.Any()) return Result.Failure(new NotFoundError("No crates found to delete"));

        var result = await _batchDeleteService.DeleteCratesAsync(crates);
        if (!result.IsSuccess)
            _logger.LogWarning("Failed to delete some crates: {CrateIds}", string.Join(", ", crates));

        return result;
    }
}