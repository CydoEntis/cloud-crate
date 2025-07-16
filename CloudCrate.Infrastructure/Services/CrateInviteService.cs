using CloudCrate.Application.Common.Errors;
using CloudCrate.Application.Common.Interfaces;
using CloudCrate.Application.Common.Models;
using CloudCrate.Domain.Entities;
using CloudCrate.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CloudCrate.Infrastructure.Services;

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

    public async Task<Result<CrateInvite>> CreateInviteAsync(Guid crateId, string invitedEmail, string invitedByUserId,
        CrateRole role, DateTime? expiresAt = null)
    {
        var invite = new CrateInvite
        {
            Id = Guid.NewGuid(),
            CrateId = crateId,
            InvitedUserEmail = invitedEmail,
            InvitedByUserId = invitedByUserId,
            Role = role,
            Token = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            Status = InviteStatus.Pending
        };

        _context.CrateInvites.Add(invite);
        await _context.SaveChangesAsync();

        var baseUrl = _config["FrontendBaseUrl"] ?? "https://cloudcrate.codystine.com";
        var inviteLink = $"{baseUrl}/invite/{invite.Token}";

        var model = new
        {
            CrateName = await GetCrateName(crateId),
            InviteLink = inviteLink
        };

        var emailResult = await _emailService.SendEmailAsync(
            invitedEmail,
            $"You have been invited to join the crate {model.CrateName} on CloudCrate!",
            "InviteUser", // Template name without extension
            model
        );

        if (!emailResult.Succeeded)
        {
            // Depending on business logic, rollback invite or just return failure
            return Result<CrateInvite>.Failure(emailResult.Errors);
        }

        return Result<CrateInvite>.Success(invite);
    }

    public async Task<Result<CrateInvite?>> GetInviteByTokenAsync(string token)
    {
        var invite = await _context.CrateInvites.FirstOrDefaultAsync(i => i.Token == token);

        if (invite == null)
            return Result<CrateInvite?>.Failure(Errors.InviteNotFound);

        return Result<CrateInvite?>.Success(invite);
    }

    public async Task<Result> AcceptInviteAsync(string token, string userId, ICrateUserRoleService roleService)
    {
        var inviteResult = await GetInviteByTokenAsync(token);

        if (!inviteResult.Succeeded || inviteResult.Data == null)
            return Result.Failure(Errors.InviteNotFound);

        var invite = inviteResult.Data;

        if (invite.Status != InviteStatus.Pending)
            return Result.Failure(Errors.InviteInvalid);

        invite.Status = InviteStatus.Accepted;

        await roleService.AssignRoleAsync(invite.CrateId, userId, invite.Role);

        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result> DeclineInviteAsync(string token)
    {
        var inviteResult = await GetInviteByTokenAsync(token);

        if (!inviteResult.Succeeded || inviteResult.Data == null)
            return Result.Failure(Errors.InviteNotFound);

        var invite = inviteResult.Data;

        if (invite.Status != InviteStatus.Pending)
            return Result.Failure(Errors.InviteInvalid);

        invite.Status = InviteStatus.Declined;
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    private async Task<string> GetCrateName(Guid crateId)
    {
        var crate = await _context.Crates.FindAsync(crateId);
        return crate?.Name ?? "Unknown Crate";
    }
}