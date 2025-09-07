using CloudCrate.Application.DTOs.Crate.Request;
using CloudCrate.Application.DTOs.Crate.Response;
using CloudCrate.Application.DTOs.Pagination;
using CloudCrate.Application.Errors;
using CloudCrate.Application.Extensions;
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
    private readonly ILogger<CrateService> _logger;

    public CrateService(
        IAppDbContext context,
        IUserService userService,
        IStorageService storageService,
        IFileService fileService,
        ICrateRoleService crateRoleService,
        ITransactionService transactionService,
        ILogger<CrateService> logger)
    {
        _context = context;
        _userService = userService;
        _storageService = storageService;
        _fileService = fileService;
        _crateRoleService = crateRoleService;
        _transactionService = transactionService;
        _logger = logger;
    }

    #region Helpers

    private async Task CollectFolderDeletionsAsync(CrateFolder folder, Guid crateId, string userId,
        List<string> keysToDelete)
    {
        foreach (var file in folder.Files)
        {
            var key = userId.GetObjectKey(crateId, folder.Id, file.Name);
            keysToDelete.Add(key);
            _context.CrateFiles.Remove(file);
        }

        foreach (var subfolder in folder.Subfolders)
            await CollectFolderDeletionsAsync(subfolder, crateId, userId, keysToDelete);
    }

    private IQueryable<Crate> GetOwnedCratesForUser(IQueryable<Crate> query, string userId) =>
        query.Where(c => c.Members.Any(m => m.UserId == userId && m.Role == CrateRole.Owner));

    private IQueryable<Crate> GetJoinedCratesForUser(IQueryable<Crate> query, string userId) =>
        query.Where(c => c.Members.Any(m => m.UserId == userId && m.Role != CrateRole.Owner));

    #endregion

    #region Public Methods

    public async Task<Result<Guid>> CreateCrateAsync(CreateCrateRequest request)
    {
        var requestedBytes = StorageSize.FromGigabytes(request.AllocatedStorageGb).Bytes;
        var canAllocate = await _userService.CanConsumeStorageAsync(request.UserId, requestedBytes);
        if (!canAllocate.IsSuccess)
        {
            _logger.LogError(
                "User {UserId} cannot allocate {RequestedBytes} bytes: {Error}",
                request.UserId, requestedBytes, canAllocate.Error?.Message
            );
            return Result<Guid>.Failure(canAllocate.Error ?? new InternalError("Storage allocation check failed"));
        }

        Crate crate;
        try
        {
            crate = Crate.Create(request.Name, request.UserId, request.AllocatedStorageGb, request.Color);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to create crate for user {UserId} with name {CrateName}",
                request.UserId, request.Name
            );
            return Result<Guid>.Failure(new ValidationError($"Invalid crate creation parameters: {ex.Message}"));
        }

        var bucketResult = await _storageService.GetOrCreateBucketAsync(crate.GetCrateStorageName());
        if (!bucketResult.IsSuccess)
        {
            _logger.LogError(
                "Failed to get or create bucket for crate {CrateId}: {Error}",
                crate.Id, bucketResult.Error?.Message
            );
            return Result<Guid>.Failure(bucketResult.Error);
        }

        // Only save the crate — root folder is already part of the crate entity
        var transactionResult = await _transactionService.ExecuteAsync(async () =>
        {
            _context.Crates.Add(crate);
            await _context.SaveChangesAsync();
        });

        if (!transactionResult.IsSuccess)
        {
            _logger.LogError(
                "Failed to save crate {CrateId} in transaction: {Error}",
                crate.Id, transactionResult.Error?.Message
            );
            return Result<Guid>.Failure(transactionResult.Error ??
                                        new InternalError("Failed to create crate in database"));
        }

        return Result<Guid>.Success(crate.Id);
    }


    public async Task<Result<bool>> AllocateCrateStorageAsync(string userId, Guid crateId, int requestedAllocationGB)
    {
        if (requestedAllocationGB < 0)
        {
            _logger.LogError("User {UserId} requested negative allocation {RequestedAllocationGB} for crate {CrateId}",
                userId, requestedAllocationGB, crateId);
            return Result<bool>.Failure(new ValidationError("Requested allocation must be >= 0"));
        }

        var canManageResult = await _crateRoleService.CanManageCrate(crateId, userId);
        if (!canManageResult.IsSuccess || !canManageResult.Value)
        {
            _logger.LogError("User {UserId} does not have permission to allocate storage for crate {CrateId}", userId,
                crateId);
            return Result<bool>.Failure(new ForbiddenError("User does not have required permissions"));
        }

        var crate = await _context.Crates.FirstOrDefaultAsync(c => c.Id == crateId);
        if (crate == null)
        {
            _logger.LogError("Crate {CrateId} not found for allocation by user {UserId}", crateId, userId);
            return Result<bool>.Failure(new NotFoundError("Crate not found"));
        }

        var requestedStorage = StorageSize.FromGigabytes(requestedAllocationGB);
        var canAllocate = await _userService.CanConsumeStorageAsync(userId, requestedStorage.Bytes);
        if (!canAllocate.IsSuccess)
        {
            _logger.LogError(
                "User {UserId} cannot consume requested storage {RequestedBytes} for crate {CrateId}: {Error}", userId,
                requestedStorage.Bytes, crateId, canAllocate.Error?.Message);
            return Result<bool>.Failure(canAllocate.Error ?? new StorageError("Failed to check quota"));
        }

        if (!crate.TryAllocateStorage(requestedAllocationGB, out var error))
        {
            _logger.LogError("Failed to allocate {RequestedGB} GB for crate {CrateId}: {Error}", requestedAllocationGB,
                crateId, error);
            return Result<bool>.Failure(new StorageError(error ?? "Failed to allocate storage"));
        }

        await _context.SaveChangesAsync();
        return Result<bool>.Success(true);
    }

    public async Task<Result<PaginatedResult<CrateListItemResponse>>> GetCratesAsync(CrateQueryParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.UserId))
        {
            _logger.LogError("Attempted to get crates with empty UserId");
            return Result<PaginatedResult<CrateListItemResponse>>.Failure(
                new UnauthorizedError("User must be logged in")
            );
        }

        try
        {
            var query = _context.Crates
                .AsNoTracking()
                .Include(c => c.Members)
                .Where(c => c.Members.Any(m => m.UserId == parameters.UserId));

            query = parameters.MemberType switch
            {
                CrateMemberType.Owner => GetOwnedCratesForUser(query, parameters.UserId),
                CrateMemberType.Joined => GetJoinedCratesForUser(query, parameters.UserId),
                _ => query
            };

            query = query.ApplySearch(parameters.SearchTerm)
                .ApplyOrdering(parameters);

            var pagedCrates = await query.PaginateAsync(parameters.Page, parameters.PageSize);

            var userIds = pagedCrates.Items
                .SelectMany(c => c.Members.Select(m => m.UserId))
                .Distinct()
                .ToList();

            var users = await _userService.GetUsersByIdsAsync(userIds);
            var userLookup = users.ToDictionary(u => u.Id, u => u);

            var crateResponses = pagedCrates.Items
                .Select(c => c.ToCrateListItemResponse(parameters.UserId, userLookup))
                .ToList();

            return Result<PaginatedResult<CrateListItemResponse>>.Success(
                PaginatedResult<CrateListItemResponse>.Create(
                    crateResponses,
                    pagedCrates.TotalCount,
                    parameters.Page,
                    parameters.PageSize
                )
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve crates for user {UserId}", parameters.UserId);
            return Result<PaginatedResult<CrateListItemResponse>>.Failure(
                new InternalError("Could not fetch crates")
            );
        }
    }


    public async Task<Result<CrateDetailsResponse>> GetCrateAsync(Guid crateId, string userId)
    {
        var canViewResult = await _crateRoleService.CanView(crateId, userId);
        if (!canViewResult.IsSuccess)
        {
            _logger.LogError(
                "User {UserId} cannot view crate {CrateId}: {Error}",
                userId, crateId, canViewResult.Error?.Message
            );
            return Result<CrateDetailsResponse>.Failure(canViewResult.Error);
        }

        var crate = await _context.Crates
            .AsNoTracking()
            .Include(c => c.Members)
            .Include(c => c.Files)
            .Include(c => c.Folders)
            .FirstOrDefaultAsync(c => c.Id == crateId);

        if (crate == null)
        {
            _logger.LogError("Crate {CrateId} not found for user {UserId}", crateId, userId);
            return Result<CrateDetailsResponse>.Failure(new NotFoundError("Crate not found"));
        }

        var member = crate.Members.FirstOrDefault(m => m.UserId == userId);
        if (member == null)
        {
            _logger.LogError("User {UserId} is not a member of crate {CrateId}", userId, crateId);
            return Result<CrateDetailsResponse>.Failure(new UnauthorizedError("User is not a member of this crate"));
        }

        var rootFolder = crate.Folders.FirstOrDefault(f => f.ParentFolderId == null);
        if (rootFolder == null)
        {
            _logger.LogError("Root folder not found for crate {CrateId}", crateId);
            return Result<CrateDetailsResponse>.Failure(new InternalError("Root folder missing"));
        }

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
        {
            _logger.LogError("User {UserId} cannot view members of crate {CrateId}", request.UserId, crateId);
            return Result<List<CrateMemberResponse>>.Failure(canViewResult.Error ??
                                                             new ForbiddenError("User cannot view crate members"));
        }

        var crate = await _context.Crates.AsNoTracking().Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == crateId);
        if (crate == null)
        {
            _logger.LogError("Crate {CrateId} not found while fetching members by user {UserId}", crateId,
                request.UserId);
            return Result<List<CrateMemberResponse>>.Failure(new NotFoundError("Crate not found"));
        }

        var memberUserIds = crate.Members.Select(m => m.UserId).Distinct().ToList();
        if (!memberUserIds.Any())
            return Result<List<CrateMemberResponse>>.Success(new List<CrateMemberResponse>());

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

    public async Task<Result> DeleteCrateAsync(Guid crateId, string userId)
    {
        var canManageResult = await _crateRoleService.CanManageCrate(crateId, userId);
        if (!canManageResult.IsSuccess || !canManageResult.Value)
        {
            _logger.LogError("User {UserId} cannot delete crate {CrateId}", userId, crateId);
            return Result.Failure(canManageResult.Error ?? new ForbiddenError("User cannot delete this crate"));
        }

        var crate = await _context.Crates
            .Include(c => c.Files)
            .Include(c => c.Folders)
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == crateId);

        if (crate == null)
        {
            _logger.LogError("Crate {CrateId} not found for deletion by user {UserId}", crateId, userId);
            return Result.Failure(new NotFoundError("Crate not found"));
        }

        var transactionResult = await _transactionService.ExecuteAsync(async () =>
        {
            foreach (var folder in crate.Folders.Where(f => f.ParentFolderId == null))
                await CollectFolderDeletionsAsync(folder, crate.Id, userId, new List<string>());

            _context.CrateFiles.RemoveRange(crate.Files);
            _context.CrateMembers.RemoveRange(crate.Members);
            _context.CrateFolders.RemoveRange(crate.Folders);
            _context.Crates.Remove(crate);

            await _context.SaveChangesAsync();
        });

        if (!transactionResult.IsSuccess)
        {
            _logger.LogError("Failed to delete crate {CrateId} in transaction: {Error}", crateId,
                transactionResult.Error?.Message);
            return transactionResult;
        }

        var deleteFilesResult = await _storageService.DeleteAllFilesInBucketAsync(crate.Id);
        if (!deleteFilesResult.IsSuccess)
        {
            _logger.LogError("Failed to delete files for crate {CrateId}: {Error}", crateId,
                deleteFilesResult.Error?.Message);
            return Result.Failure(deleteFilesResult.Error);
        }

        var bucketDeleteResult = await _storageService.DeleteBucketAsync(crate.Id);
        if (!bucketDeleteResult.IsSuccess)
        {
            _logger.LogError("Failed to delete bucket for crate {CrateId}: {Error}", crateId,
                bucketDeleteResult.Error?.Message);
            return Result.Failure(bucketDeleteResult.Error);
        }

        return Result.Success();
    }

    public async Task<Result> LeaveCrateAsync(Guid crateId, string userId)
    {
        var canLeaveResult = await _crateRoleService.CanView(crateId, userId);
        if (!canLeaveResult.IsSuccess || !canLeaveResult.Value)
        {
            _logger.LogError("User {UserId} cannot leave crate {CrateId}", userId, crateId);
            return Result.Failure(canLeaveResult.Error ?? new ForbiddenError("User cannot leave this crate"));
        }

        var crate = await _context.Crates.Include(c => c.Members).FirstOrDefaultAsync(c => c.Id == crateId);
        if (crate == null)
        {
            _logger.LogError("Crate {CrateId} not found for leaving by user {UserId}", crateId, userId);
            return Result.Failure(new NotFoundError("Crate not found"));
        }

        var member = crate.Members.FirstOrDefault(m => m.UserId == userId);
        if (member == null)
        {
            _logger.LogError("Membership not found in crate {CrateId} for user {UserId}", crateId, userId);
            return Result.Failure(new NotFoundError("Membership not found"));
        }

        if (member.Role == CrateRole.Owner)
        {
            _logger.LogError("Owner {UserId} cannot leave crate {CrateId}", userId, crateId);
            return Result.Failure(new ValidationError("Owner cannot leave the crate"));
        }

        _context.CrateMembers.Remove(member);
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<CrateListItemResponse>> UpdateCrateAsync(Guid crateId, string userId, string? newName,
        string? newColor)
    {
        var canManageResult = await _crateRoleService.CanManageCrate(crateId, userId);
        if (!canManageResult.IsSuccess || !canManageResult.Value)
        {
            return Result<CrateListItemResponse>.Failure(
                canManageResult.Error ?? new ForbiddenError("User cannot update this crate")
            );
        }

        var crate = await _context.Crates
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == crateId);

        if (crate == null)
        {
            return Result<CrateListItemResponse>.Failure(new NotFoundError("Crate not found"));
        }

        if (!string.IsNullOrWhiteSpace(newName))
            crate.Rename(newName);

        if (!string.IsNullOrWhiteSpace(newColor))
            crate.SetColor(newColor);

        await _context.SaveChangesAsync();

        var userIds = crate.Members.Select(m => m.UserId).Distinct().ToList();
        var users = await _userService.GetUsersByIdsAsync(userIds);
        var userLookup = users.ToDictionary(u => u.Id, u => u);

        return Result<CrateListItemResponse>.Success(crate.ToCrateListItemResponse(userId, userLookup));
    }

    #endregion
}