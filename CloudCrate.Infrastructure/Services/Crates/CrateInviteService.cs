using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.Invite.Response;
using CloudCrate.Application.Email.Models;
using CloudCrate.Application.Interfaces.Crate;
using CloudCrate.Application.Interfaces.Email;
using CloudCrate.Application.Interfaces.Persistence;
using CloudCrate.Domain.Entities;
using CloudCrate.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CloudCrate.Infrastructure.Services.Crates;

public class CrateInviteService : ICrateInviteService
{
    private readonly IAppDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;

    public CrateInviteService(IAppDbContext context, IEmailService emailService, IConfiguration config)
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
            return Result.Failure(new NotFoundError("Crate not found", nameof(Domain.Entities.Crate), crateId.ToString()));

        var invite = CrateInvite.Create(crateId, invitedEmail, invitedByUserId, role, expiresAt);

        _context.CrateInvites.Add(invite);
        await _context.SaveChangesAsync();

        var emailResult = await SendInviteEmailAsync(invitedEmail, crateName, invite.Token);
        if (emailResult.IsFailure)
            return Result.Failure(new EmailSendError($"Failed to send invite email to {invitedEmail}"));

        return Result.Success();
    }

    public async Task<Result<CrateInviteDetailsResponse>> GetInviteByTokenAsync(string token)
    {
        var invite = await _context.CrateInvites
            .Include(i => i.Crate)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Token == token);

        if (invite == null)
            return Result<CrateInviteDetailsResponse>.Failure(new NotFoundError("Invite not found", nameof(CrateInvite)));

        return Result<CrateInviteDetailsResponse>.Success(CrateInviteDetailsResponse.FromEntity(invite));
    }

    public async Task<Result> AcceptInviteAsync(string token, string userId, ICrateMemberService roleService)
    {
        var invite = await GetTrackedInviteByTokenAsync(token);
        if (invite == null)
            return Result.Failure(new NotFoundError("Invite not found", nameof(CrateInvite)));

        if (invite.Status != InviteStatus.Pending)
            return Result.Failure(new BusinessRuleError("Invite is not pending"));

        if (invite.ExpiresAt.HasValue && invite.ExpiresAt.Value < DateTime.UtcNow)
            return Result.Failure(new BusinessRuleError("Invite has expired"));

        invite.UpdateInviteStatus(InviteStatus.Accepted);

        var roleResult = await roleService.AssignRoleAsync(invite.CrateId, userId, invite.Role);
        if (roleResult.IsFailure)
            return Result.Failure(new InternalError("Failed to assign crate role to user"));

        await _context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> DeclineInviteAsync(string token)
    {
        var invite = await GetTrackedInviteByTokenAsync(token);
        if (invite == null)
            return Result.Failure(new NotFoundError("Invite not found", nameof(CrateInvite)));

        if (invite.Status != InviteStatus.Pending)
            return Result.Failure(new BusinessRuleError("Invite is not pending"));

        if (invite.ExpiresAt.HasValue && invite.ExpiresAt.Value < DateTime.UtcNow)
            return Result.Failure(new BusinessRuleError("Invite has expired"));

        invite.UpdateInviteStatus(InviteStatus.Declined);
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    private async Task<CrateInvite?> GetTrackedInviteByTokenAsync(string token)
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

        return result.IsSuccess ? Result.Success() : Result.Failure(new EmailSendError($"Failed to send invite email to {email}"));
    }
}
