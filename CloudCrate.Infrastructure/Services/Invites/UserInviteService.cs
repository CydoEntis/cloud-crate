using CloudCrate.Application.DTOs.Invite.Request;
using CloudCrate.Application.DTOs.Invite.Response;
using CloudCrate.Application.Errors;
using CloudCrate.Application.Interfaces.Invite;
using CloudCrate.Application.Models;
using CloudCrate.Domain.Entities;
using CloudCrate.Infrastructure.Persistence;
using CloudCrate.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CloudCrate.Infrastructure.Services.Invites;

public class UserInviteService : IUserInviteService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UserInviteService> _logger;

    public UserInviteService(
        AppDbContext context,
        IConfiguration configuration,
        ILogger<UserInviteService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Result<InviteUserResponse>> CreateInviteAsync(string createdByUserId,
        CreateUserInviteRequest request)
    {
        try
        {
            var inviteDomain = InviteToken.Create(
                createdByUserId: createdByUserId,
                email: request.Email,
                expiryHours: request.ExpiryHours
            );

            var inviteEntity = inviteDomain.ToEntity();
            _context.InviteTokens.Add(inviteEntity);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Invite token created by user {UserId} for email {Email}",
                createdByUserId, request.Email ?? "any");

            var frontendUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:5173";
            var inviteUrl = $"{frontendUrl}/register?inviteToken={inviteDomain.Token}";

            var response = new InviteUserResponse
            {
                Id = inviteDomain.Id,
                Token = inviteDomain.Token,
                Email = inviteDomain.Email,
                CreatedByUserId = inviteDomain.CreatedByUserId,
                CreatedAt = inviteDomain.CreatedAt,
                ExpiresAt = inviteDomain.ExpiresAt,
                UsedAt = inviteDomain.UsedAt,
                UsedByUserId = inviteDomain.UsedByUserId,
                IsUsed = inviteDomain.IsUsed,
                IsExpired = inviteDomain.IsExpired,
                IsValid = inviteDomain.IsValid,
                InviteUrl = inviteUrl
            };

            return Result<InviteUserResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create invite token for user {UserId}", createdByUserId);
            return Result<InviteUserResponse>.Failure(Error.Internal("Failed to create invite"));
        }
    }

    public async Task<Result<InviteToken>> ValidateInviteTokenAsync(string token)
    {
        try
        {
            var inviteEntity = await _context.InviteTokens
                .FirstOrDefaultAsync(i => i.Token == token);

            if (inviteEntity == null)
            {
                _logger.LogWarning("Invalid invite token attempted: {Token}", token);
                return Result<InviteToken>.Failure(Error.NotFound("Invalid invite token"));
            }

            var inviteDomain = inviteEntity.ToDomain();

            if (!inviteDomain.IsValid)
            {
                var reason = inviteDomain.IsUsed ? "already used" : "expired";
                _logger.LogWarning("Invalid invite token {Token}: {Reason}", token, reason);
                return Result<InviteToken>.Failure(Error.Unauthorized($"Invite token has {reason}"));
            }

            return Result<InviteToken>.Success(inviteDomain);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating invite token {Token}", token);
            return Result<InviteToken>.Failure(Error.Internal("Failed to validate invite token"));
        }
    }

    public async Task<Result> MarkInviteAsUsedAsync(string token, string usedByUserId)
    {
        try
        {
            var inviteEntity = await _context.InviteTokens
                .FirstOrDefaultAsync(i => i.Token == token);

            if (inviteEntity == null)
            {
                return Result.Failure(Error.NotFound("Invalid invite token"));
            }

            var inviteDomain = inviteEntity.ToDomain();

            try
            {
                inviteDomain.MarkAsUsed(usedByUserId);

                inviteEntity.UsedAt = inviteDomain.UsedAt;
                inviteEntity.UsedByUserId = inviteDomain.UsedByUserId;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Invite token {Token} marked as used by user {UserId}",
                    token, usedByUserId);

                return Result.Success();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Failed to mark invite as used: {Error}", ex.Message);
                return Result.Failure(Error.Unauthorized(ex.Message));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking invite token {Token} as used", token);
            return Result.Failure(Error.Internal("Failed to mark invite as used"));
        }
    }

    public async Task<Result<IEnumerable<InviteUserResponse>>> GetInvitesByUserAsync(string userId)
    {
        try
        {
            var inviteEntities = await _context.InviteTokens
                .Where(i => i.CreatedByUserId == userId)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            var frontendUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:5173";

            var responses = inviteEntities.Select(entity =>
            {
                var domain = entity.ToDomain();
                return new InviteUserResponse
                {
                    Id = domain.Id,
                    Token = domain.Token,
                    Email = domain.Email,
                    CreatedByUserId = domain.CreatedByUserId,
                    CreatedAt = domain.CreatedAt,
                    ExpiresAt = domain.ExpiresAt,
                    UsedAt = domain.UsedAt,
                    UsedByUserId = domain.UsedByUserId,
                    IsUsed = domain.IsUsed,
                    IsExpired = domain.IsExpired,
                    IsValid = domain.IsValid,
                    InviteUrl = $"{frontendUrl}/register?inviteToken={domain.Token}"
                };
            });

            return Result<IEnumerable<InviteUserResponse>>.Success(responses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving invites for user {UserId}", userId);
            return Result<IEnumerable<InviteUserResponse>>.Failure(
                Error.Internal("Failed to retrieve invites"));
        }
    }

    public async Task<Result> DeleteExpiredInvitesAsync()
    {
        try
        {
            var expiredInvites = await _context.InviteTokens
                .Where(i => i.ExpiresAt < DateTime.UtcNow)
                .ToListAsync();

            if (expiredInvites.Count > 0)
            {
                _context.InviteTokens.RemoveRange(expiredInvites);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted {Count} expired invite tokens", expiredInvites.Count);
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting expired invite tokens");
            return Result.Failure(Error.Internal("Failed to delete expired invites"));
        }
    }

    public async Task<Result<InviteUserStatsResponse>> GetInviteStatsAsync(string userId)
    {
        try
        {
            var invites = await _context.InviteTokens
                .Where(i => i.CreatedByUserId == userId)
                .ToListAsync();

            var stats = new InviteUserStatsResponse
            {
                TotalInvites = invites.Count,
                UsedInvites = invites.Count(i => i.UsedAt.HasValue),
                ExpiredInvites = invites.Count(i => i.ExpiresAt < DateTime.UtcNow && !i.UsedAt.HasValue),
                ActiveInvites = invites.Count(i => i.ExpiresAt > DateTime.UtcNow && !i.UsedAt.HasValue),
                LastInviteCreated = invites.MaxBy(i => i.CreatedAt)?.CreatedAt,
                LastInviteUsed = invites.Where(i => i.UsedAt.HasValue).MaxBy(i => i.UsedAt)?.UsedAt
            };

            return Result<InviteUserStatsResponse>.Success(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving invite stats for user {UserId}", userId);
            return Result<InviteUserStatsResponse>.Failure(
                Error.Internal("Failed to retrieve invite statistics"));
        }
    }
}