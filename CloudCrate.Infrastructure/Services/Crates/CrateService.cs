using CloudCrate.Application.Common.Utils;
using CloudCrate.Application.DTOs.Crate.Request;
using CloudCrate.Application.DTOs.Crate.Response;
using CloudCrate.Application.DTOs.Pagination;
using CloudCrate.Application.DTOs.User.Response;
using CloudCrate.Application.Errors;
using CloudCrate.Application.Extensions;
using CloudCrate.Application.Interfaces;
using CloudCrate.Application.Interfaces.Crate;
using CloudCrate.Application.Interfaces.File;
using CloudCrate.Application.Interfaces.Storage;
using CloudCrate.Application.Interfaces.User;
using CloudCrate.Application.Interfaces.Permissions;
using CloudCrate.Application.Interfaces.Transactions;
using CloudCrate.Application.Mappers;
using CloudCrate.Application.Models;
using CloudCrate.Domain.Entities;
using CloudCrate.Domain.Enums;
using CloudCrate.Domain.ValueObjects;
using CloudCrate.Infrastructure.Persistence;
using CloudCrate.Infrastructure.Persistence.Mappers;
using CloudCrate.Infrastructure.Queries;
using CloudCrate.Infrastructure.Services.Files;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;


namespace CloudCrate.Infrastructure.Services.Crates;

public class CrateService : ICrateService
{
    private readonly AppDbContext _context;
    private readonly IUserService _userService;
    private readonly IStorageService _storageService;
    private readonly ICrateRoleService _crateRoleService;
    private readonly ITransactionService _transactionService;
    private readonly IBatchDeleteService _batchDeleteService;
    private readonly ILogger<CrateService> _logger;


    public CrateService(
        AppDbContext context,
        IUserService userService,
        IStorageService storageService,
        ICrateRoleService crateRoleService,
        ITransactionService transactionService,
        IBatchDeleteService batchDeleteService,
        ILogger<CrateService> logger
    )
    {
        _context = context;
        _userService = userService;
        _storageService = storageService;
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
            _logger.LogWarning("User {UserId} cannot allocate {RequestedBytes} bytes: {Error}",
                request.UserId, requestedBytes, canAllocate.Error?.Message);

            return Result<Guid>.Failure(
                canAllocate.Error ?? new InternalError("Storage allocation check failed"));
        }

        Crate crateDomain;
        try
        {
            crateDomain = Crate.Create(
                name: request.Name,
                userId: request.UserId,
                allocatedGb: request.AllocatedStorageGb,
                color: request.Color
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create crate with name {CrateName}", request.Name);
            return Result<Guid>.Failure(new ValidationError(
                $"Invalid crate creation parameters: {ex.Message}"));
        }

        var bucketResult = await _storageService.EnsureBucketExistsAsync();
        if (!bucketResult.IsSuccess)
        {
            _logger.LogError("Failed to ensure main bucket exists: {Error}", bucketResult.Error?.Message);
            return Result<Guid>.Failure(bucketResult.Error);
        }

        var crateEntity = crateDomain.ToEntity();

        var transactionResult = await _transactionService.ExecuteAsync(async () =>
        {
            _context.Crates.Add(crateEntity);
            await _context.SaveChangesAsync();
        });

        if (!transactionResult.IsSuccess)
        {
            _logger.LogError("Failed to save crate {CrateId} in transaction: {Error}",
                crateDomain.Id, transactionResult.Error?.Message);

            await _storageService.DeleteAllFilesForCrateAsync(crateDomain.Id);

            return Result<Guid>.Failure(transactionResult.Error ??
                                        new InternalError("Failed to create crate in database"));
        }

        return Result<Guid>.Success(crateDomain.Id);
    }


    public async Task<Result<bool>> AllocateCrateStorageAsync(string userId, Guid crateId, int requestedAllocationGB)
    {
        if (requestedAllocationGB < 0)
            return Result<bool>.Failure(new ValidationError("Requested allocation must be >= 0"));

        var canManageResult = await _crateRoleService.CanManageCrate(crateId, userId);
        if (!canManageResult.IsSuccess || !canManageResult.Value)
            return Result<bool>.Failure(new ForbiddenError("User does not have required permissions"));

        var crateEntity = await _context.Crates
            .Include(c => c.Folders)
            .Include(c => c.Files)
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == crateId);

        if (crateEntity == null)
            return Result<bool>.Failure(new NotFoundError("Crate not found"));

        var crateDomain = crateEntity.ToDomain();

        var requestedStorage = StorageSize.FromGigabytes(requestedAllocationGB);
        var canAllocate = await _userService.CanConsumeStorageAsync(userId, requestedStorage.Bytes);
        if (!canAllocate.IsSuccess)
            return Result<bool>.Failure(canAllocate.Error ?? new StorageError("Failed to check quota"));

        if (!crateDomain.TryAllocateStorage(requestedAllocationGB, out var error))
            return Result<bool>.Failure(new StorageError(error ?? "Failed to allocate storage"));

        crateEntity.UpdateEntity(crateDomain);

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

            query = query.ApplySearch(parameters.SearchTerm).ApplyOrdering(parameters);

            var pagedEntities = await query.PaginateAsync(parameters.Page, parameters.PageSize);

            var domainCrates = pagedEntities.Items.Select(e => e.ToDomain()).ToList();

            var userLookup = pagedEntities.Items
                .SelectMany(c => c.Members)
                .Where(m => m.User != null)
                .Select(m => m.User!)
                .DistinctBy(u => u.Id)
                .ToDictionary(
                    u => u.Id,
                    u => new UserResponse
                    {
                        Id = u.Id,
                        DisplayName = u.DisplayName,
                        Email = u.Email,
                        ProfilePictureUrl = u.ProfilePictureUrl
                    });

            var crateResponses = domainCrates
                .Select(c => c.ToCrateListItemResponse(parameters.UserId, userLookup))
                .ToList();


            return Result<PaginatedResult<CrateListItemResponse>>.Success(
                PaginatedResult<CrateListItemResponse>.Create(
                    crateResponses,
                    pagedEntities.TotalCount,
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
            return Result<CrateDetailsResponse>.Failure(canViewResult.Error);

        var crateEntity = await _context.Crates.AsNoTracking()
            .Include(c => c.Members)
            .Include(c => c.Files)
            .Include(c => c.Folders)
            .FirstOrDefaultAsync(c => c.Id == crateId);

        if (crateEntity is null)
            return Result<CrateDetailsResponse>.Failure(new NotFoundError("Crate not found"));

        var crateDomain = crateEntity.ToDomain();

        var member = crateDomain.Members.FirstOrDefault(m => m.UserId == userId);
        if (member is null)
            return Result<CrateDetailsResponse>.Failure(
                new UnauthorizedError("User is not a member of this crate"));

        var rootFolder = crateDomain.Folders.FirstOrDefault(f => f.ParentFolderId == null);
        if (rootFolder is null)
            return Result<CrateDetailsResponse>.Failure(
                new InternalError("Root folder missing"));

        var fileBreakdown = FileBreakdownHelper.GetFilesByMimeTypeInMemory(crateDomain.Files);

        var response = new CrateDetailsResponse
        {
            Id = crateDomain.Id,
            Name = crateDomain.Name,
            Color = crateDomain.Color,
            Role = member.Role,
            TotalUsedStorage = crateDomain.UsedStorage.Bytes,
            StorageLimit = crateDomain.AllocatedStorage.Bytes,
            BreakdownByType = fileBreakdown,
            RootFolderId = rootFolder.Id
        };

        return Result<CrateDetailsResponse>.Success(response);
    }


    public async Task<Result<List<CrateMemberResponse>>> GetCrateMembersAsync(Guid crateId, CrateMemberRequest request)
    {
        var canViewResult = await _crateRoleService.CanView(crateId, request.UserId);
        if (!canViewResult.IsSuccess || !canViewResult.Value)
            return Result<List<CrateMemberResponse>>.Failure(
                canViewResult.Error ?? new ForbiddenError("User cannot view crate members"));

        var crateEntity = await _context.Crates.AsNoTracking()
            .Include(c => c.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(c => c.Id == crateId);

        if (crateEntity is null)
            return Result<List<CrateMemberResponse>>.Failure(new NotFoundError("Crate not found"));

        var crateDomain = crateEntity.ToDomain();

        var userLookup = crateEntity.Members
            .Where(m => m.User != null)
            .Select(m => m.User!)
            .DistinctBy(u => u.Id)
            .ToDictionary(
                u => u.Id,
                u => new UserResponse
                {
                    Id = u.Id,
                    DisplayName = u.DisplayName,
                    Email = u.Email,
                    ProfilePictureUrl = u.ProfilePictureUrl
                });

        var responses = crateDomain.Members
            .Select(m => m.ToCrateMemberResponse(userLookup))
            .ToList();

        return Result<List<CrateMemberResponse>>.Success(responses);
    }


    public async Task<Result<CrateListItemResponse>> UpdateCrateAsync(
        Guid crateId,
        string userId,
        string? newName,
        string? newColor)
    {
        var canManageResult = await _crateRoleService.CanManageCrate(crateId, userId);
        if (!canManageResult.IsSuccess || !canManageResult.Value)
            return Result<CrateListItemResponse>.Failure(
                canManageResult.Error ?? new ForbiddenError("User cannot update this crate"));

        var crateEntity = await _context.Crates
            .Include(c => c.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(c => c.Id == crateId);

        if (crateEntity is null)
            return Result<CrateListItemResponse>.Failure(new NotFoundError("Crate not found"));

        var crateDomain = crateEntity.ToDomain();

        if (!string.IsNullOrWhiteSpace(newName))
            crateDomain.Rename(newName);

        if (!string.IsNullOrWhiteSpace(newColor))
            crateDomain.SetColor(newColor);

        crateEntity.UpdateEntity(crateDomain);

        await _context.SaveChangesAsync();

        var userLookup = crateEntity.Members
            .Where(m => m.User != null)
            .Select(m => m.User!)
            .DistinctBy(u => u.Id)
            .ToDictionary(
                u => u.Id,
                u => new UserResponse
                {
                    Id = u.Id,
                    DisplayName = u.DisplayName,
                    Email = u.Email,
                    ProfilePictureUrl = u.ProfilePictureUrl
                });

        var response = crateDomain.ToCrateListItemResponse(userId, userLookup);

        return Result<CrateListItemResponse>.Success(response);
    }


    public async Task<Result> DeleteCrateAsync(Guid crateId, string userId)
    {
        var crateEntity = await _context.Crates
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == crateId);

        if (crateEntity is null)
            return Result.Failure(new NotFoundError("Crate not found"));

        var crateDomain = crateEntity.ToDomain();

        var isOwner = crateDomain.Members.Any(m => m.UserId == userId && m.Role == CrateRole.Owner);
        if (!isOwner)
            return Result.Failure(new ForbiddenError("You do not have permission to delete this crate"));

        var result = await _batchDeleteService.DeleteCratesAsync(new[] { crateDomain.Id });
        if (!result.IsSuccess)
            _logger.LogWarning("Failed to delete crate {CrateId}", crateId);

        return result;
    }


    public async Task<Result> DeleteCratesAsync(IEnumerable<Guid> crateIds, string userId)
    {
        var crateEntities = await _context.Crates
            .Include(c => c.Members)
            .Where(c => crateIds.Contains(c.Id))
            .ToListAsync();

        if (!crateEntities.Any())
            return Result.Failure(new NotFoundError("No crates found"));

        var domainCrates = crateEntities.Select(c => c.ToDomain()).ToList();

        var ownedCrates = domainCrates
            .Where(c => c.Members.Any(m => m.UserId == userId && m.Role == CrateRole.Owner))
            .Select(c => c.Id)
            .ToList();

        if (!ownedCrates.Any())
            return Result.Failure(new ForbiddenError("You do not have permission to delete these crates"));

        var result = await _batchDeleteService.DeleteCratesAsync(ownedCrates);
        if (!result.IsSuccess)
            _logger.LogWarning("Failed to delete some crates: {CrateIds}", string.Join(", ", ownedCrates));

        return result;
    }
}