using CloudCrate.Application.DTOs.Invite.Response;
using CloudCrate.Application.Email.Models;
using CloudCrate.Application.Errors;
using CloudCrate.Application.Interfaces.Crate;
using CloudCrate.Application.Interfaces.Email;
using CloudCrate.Application.Models;
using CloudCrate.Domain.Entities;
using CloudCrate.Domain.Enums;
using CloudCrate.Infrastructure.Persistence;
using CloudCrate.Infrastructure.Persistence.Entities;
using CloudCrate.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CloudCrate.Infrastructure.Services.Crates;

public class CrateInviteService : ICrateInviteService
{
    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;

    public CrateInviteService(AppDbContext context, IEmailService emailService, IConfiguration config)
    {
        _context = context;
        _emailService = emailService;
        _config = config;
    }

    public async Task<Result> CreateInviteAsync(
        Guid crateId,
        string invitedEmail,
        string invitedByUserId,
        CrateRole role,
        DateTime? expiresAt = null)
    {
        invitedEmail = invitedEmail.Trim().ToLowerInvariant();

        var existingInvite = await _context.CrateInvites
            .FirstOrDefaultAsync(i =>
                i.CrateId == crateId &&
                i.InvitedUserEmail == invitedEmail &&
                i.Status == InviteStatus.Pending);

        if (existingInvite != null)
            return Result.Failure(new AlreadyExistsError("An active invite for this email already exists"));

        var crateName = await GetCrateName(crateId);
        if (string.IsNullOrWhiteSpace(crateName))
            return Result.Failure(new NotFoundError("Crate not found", nameof(Domain.Entities.Crate),
                crateId.ToString()));

        var inviteDomain = CrateInvite.Create(crateId, invitedEmail, invitedByUserId, role, expiresAt);
        var inviteEntity = inviteDomain.ToEntity(crateId);

        _context.CrateInvites.Add(inviteEntity);
        await _context.SaveChangesAsync();

        var emailResult = await SendInviteEmailAsync(invitedEmail, crateName, inviteDomain.Token);
        if (emailResult.IsFailure)
            return Result.Failure(new EmailSendError($"Failed to send invite email to {invitedEmail}"));

        return Result.Success();
    }

    public async Task<Result<CrateInviteDetailsResponse>> GetInviteByTokenAsync(string token)
    {
        var inviteEntity = await _context.CrateInvites
            .Include(i => i.Crate)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Token == token);

        if (inviteEntity == null)
            return Result<CrateInviteDetailsResponse>.Failure(
                new NotFoundError("Invite not found", nameof(CrateInvite))
            );

        var inviteDomain = inviteEntity.ToDomain();
        return Result<CrateInviteDetailsResponse>.Success(
            CrateInviteDetailsResponse.FromDomain(inviteDomain)
        );
    }


    public async Task<Result> AcceptInviteAsync(string token, string userId, ICrateMemberService roleService)
    {
        var inviteEntity = await GetTrackedInviteByTokenAsync(token);
        if (inviteEntity == null)
            return Result.Failure(new NotFoundError("Invite not found", nameof(CrateInvite)));

        var inviteDomain = inviteEntity.ToDomain();

        if (inviteDomain.Status != InviteStatus.Pending)
            return Result.Failure(new BusinessRuleError("Invite is not pending"));

        if (inviteDomain.ExpiresAt.HasValue && inviteDomain.ExpiresAt.Value < DateTime.UtcNow)
            return Result.Failure(new BusinessRuleError("Invite has expired"));

        inviteDomain.UpdateInviteStatus(InviteStatus.Accepted);

        var roleResult = await roleService.AssignRoleAsync(
            inviteDomain.CrateId,
            userId,
            inviteDomain.Role,
            inviteDomain.InvitedByUserId
        );
        if (roleResult.IsFailure)
            return Result.Failure(new InternalError("Failed to assign crate role to user"));

        inviteEntity.Status = inviteDomain.Status;
        inviteEntity.UpdatedAt = inviteDomain.UpdatedAt;

        await _context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> DeclineInviteAsync(string token)
    {
        var inviteEntity = await GetTrackedInviteByTokenAsync(token);
        if (inviteEntity == null)
            return Result.Failure(new NotFoundError("Invite not found", nameof(CrateInvite)));

        var inviteDomain = inviteEntity.ToDomain();

        if (inviteDomain.Status != InviteStatus.Pending)
            return Result.Failure(new BusinessRuleError("Invite is not pending"));

        if (inviteDomain.ExpiresAt.HasValue && inviteDomain.ExpiresAt.Value < DateTime.UtcNow)
            return Result.Failure(new BusinessRuleError("Invite has expired"));

        inviteDomain.UpdateInviteStatus(InviteStatus.Declined);

        inviteEntity.Status = inviteDomain.Status;
        inviteEntity.UpdatedAt = inviteDomain.UpdatedAt;

        await _context.SaveChangesAsync();
        return Result.Success();
    }

    private async Task<CrateInviteEntity?> GetTrackedInviteByTokenAsync(string token)
    {
        return await _context.CrateInvites.FirstOrDefaultAsync(i => i.Token == token);
    }

    private async Task<string?> GetCrateName(Guid crateId)
    {
        var crate = await _context.Crates.AsNoTracking().FirstOrDefaultAsync(c => c.Id == crateId);
        return crate?.Name;
    }

    private string BuildInviteLink(string token)
    {
        var baseUrl = _config["Email:clientUrl"] ?? "https://cloudcrate.codystine.com";
        return $"{baseUrl.TrimEnd('/')}/invite/{token}";
    }

    private async Task<Result> SendInviteEmailAsync(string email, string crateName, string token)
    {
        var inviteLink = BuildInviteLink(token);
        var subject = $"You have been invited to join {crateName} on CloudCrate!";
        var model = new InviteEmailModel(crateName, inviteLink);

        var result = await _emailService.SendEmailAsync(
            email,
            subject,
            templateName: "InviteEmail",
            model: model
        );

        return result.IsSuccess
            ? Result.Success()
            : Result.Failure(new EmailSendError($"Failed to send invite email to {email}"));
    }
}