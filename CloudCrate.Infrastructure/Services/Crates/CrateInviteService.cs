using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Models;
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

        if (existingInvite is not null)
            return Result.Failure(Errors.Invites.AlreadyExists);

        var crateName = await GetCrateName(crateId);
        if (string.IsNullOrWhiteSpace(crateName))
            return Result.Failure(Errors.Crates.NotFound);

        var invite = CrateInvite.Create(crateId, invitedEmail, invitedByUserId, role, expiresAt);

        _context.CrateInvites.Add(invite);
        await _context.SaveChangesAsync();

        var emailResult = await SendInviteEmailAsync(invitedEmail, crateName, invite.Token);
        if (!emailResult.Succeeded)
            return Result.Failure(emailResult.Errors);

        return Result.Success();
    }


    public async Task<Result<CrateInvite>> GetInviteByTokenAsync(string token)
    {
        var invite = await _context.CrateInvites
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Token == token);

        if (invite is null)
            return Result<CrateInvite>.Failure(Errors.Invites.NotFound);

        return Result<CrateInvite>.Success(invite);
    }

    public async Task<Result> AcceptInviteAsync(string token, string userId, ICrateUserRoleService roleService)
    {
        var inviteResult = await GetValidPendingInvite(token);
        if (!inviteResult.Succeeded)
            return Result.Failure(inviteResult.Errors);

        var invite = inviteResult.Value;

        invite.UpdateInviteStatus(InviteStatus.Accepted);

        var roleResult = await roleService.AssignRoleAsync(invite.CrateId, userId, invite.Role);
        if (!roleResult.Succeeded)
            return Result.Failure(roleResult.Errors);

        await _context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> DeclineInviteAsync(string token)
    {
        var inviteResult = await GetValidPendingInvite(token);
        if (!inviteResult.Succeeded)
            return Result.Failure(inviteResult.Errors);

        inviteResult.Value.UpdateInviteStatus(InviteStatus.Declined);
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    private async Task<string?> GetCrateName(Guid crateId)
    {
        var crate = await _context.Crates.FindAsync(crateId);
        return crate?.Name;
    }

    private string BuildInviteLink(string token)
    {
        var baseUrl = _config["Email:clientUrl"] ?? "https://cloudcrate.codystine.com";
        return $"{baseUrl}/invite/{token}";
    }

    private async Task<Result> SendInviteEmailAsync(string email, string crateName, string token)
    {
        var inviteLink = BuildInviteLink(token);
        var subject = $"You have been invited to join {crateName} on CloudCrate!";
        var model = new InviteEmailModel(crateName, inviteLink);

        return await _emailService.SendEmailAsync(
            email,
            subject,
            templateName: "InviteEmail",
            model: model
        );
    }


    private async Task<Result<CrateInvite>> GetValidPendingInvite(string token)
    {
        var inviteResult = await GetInviteByTokenAsync(token);
        if (!inviteResult.Succeeded || inviteResult.Value is null)
            return Result<CrateInvite>.Failure(Errors.Invites.NotFound);

        var invite = inviteResult.Value;

        if (invite.Status != InviteStatus.Pending)
            return Result<CrateInvite>.Failure(Errors.Invites.Invalid);

        if (invite.ExpiresAt.HasValue && invite.ExpiresAt.Value < DateTime.UtcNow)
            return Result<CrateInvite>.Failure(Errors.Invites.Expired);

        return Result<CrateInvite>.Success(invite);
    }
}