using CloudCrate.Application.DTOs.Invite.Request;
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
    private readonly ICrateService _crateService;
    private readonly ICrateMemberService _crateMemberService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;

    public CrateInviteService(
        AppDbContext context,
        IEmailService emailService,
        IConfiguration config,
        ICrateService crateService,
        ICrateMemberService crateMemberService)
    {
        _context = context;
        _crateService = crateService;
        _crateMemberService = crateMemberService;
        _emailService = emailService;
        _config = config;
    }

    private async Task<bool> HasPendingInvite(Guid crateId, string invitedEmail)
    {
        invitedEmail = invitedEmail.Trim().ToLowerInvariant();

        var existingInvite = await _context.CrateInvites
            .FirstOrDefaultAsync(i =>
                i.CrateId == crateId &&
                i.InvitedUserEmail == invitedEmail &&
                i.Status == InviteStatus.Pending);

        return existingInvite is not null;
    }

    public async Task<Result> CreateInviteAsync(string userId, CrateInviteRequest request)
    {
        var hasPendingInvite = await HasPendingInvite(request.CrateId, request.InvitedEmail);
        if (hasPendingInvite)
            return Result.Failure(new AlreadyExistsError("An active invite for this email already exists"));

        var crateNameResult = await _crateService.GetCrateNameAsync(request.CrateId);
        if (crateNameResult.IsFailure)
            return Result.Failure(crateNameResult.GetError());

        var expirationDate = DateTime.UtcNow.AddMinutes(15);

        var inviteDomain = CrateInvite.Create(request.CrateId, request.InvitedEmail, userId,
            request.Role, expirationDate);
        var inviteEntity = inviteDomain.ToEntity();

        _context.CrateInvites.Add(inviteEntity);
        await _context.SaveChangesAsync();

        var emailResult =
            await SendInviteEmailAsync(inviteDomain.InvitedUserEmail, crateNameResult.GetValue(), inviteDomain.Token);
        if (emailResult.IsFailure)
            return Result.Failure(
                new EmailSendError($"Failed to send invite email to {inviteDomain.InvitedUserEmail}"));

        return Result.Success();
    }

    public async Task<Result> AcceptInviteAsync(string token, string userId)
    {
        var inviteEntity = await GetInviteEntityByTokenAsync(token);
        if (inviteEntity == null)
            return Result.Failure(new NotFoundError("Invite not found", nameof(CrateInvite)));

        var inviteDomain = inviteEntity.ToDomain();

        if (inviteDomain.Status != InviteStatus.Pending)
            return Result.Failure(new BusinessRuleError("Invite has expired or does not exist"));

        if (inviteDomain.ExpiresAt.HasValue && inviteDomain.ExpiresAt.Value < DateTime.UtcNow)
            return Result.Failure(new BusinessRuleError("Invite has expired"));

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var roleResult = await _crateMemberService.AcceptInviteRoleAsync(
                inviteDomain.CrateId,
                userId,
                inviteDomain.Role,
                inviteDomain.InvitedByUserId
            );

            if (roleResult.IsFailure)
            {
                await transaction.RollbackAsync();
                return Result.Failure(roleResult.GetError());
            }

            inviteDomain.UpdateInviteStatus(InviteStatus.Accepted);

            inviteEntity.Status = inviteDomain.Status;
            inviteEntity.UpdatedAt = inviteDomain.UpdatedAt;

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return Result.Failure(new InternalError($"Failed to accept invite: {ex.Message}"));
        }
    }

    public async Task<Result> DeclineInviteAsync(string token)
    {
        var inviteEntity = await GetInviteEntityByTokenAsync(token);
        if (inviteEntity == null)
            return Result.Failure(new NotFoundError("Invite not found", nameof(CrateInvite)));

        var inviteDomain = inviteEntity.ToDomain();

        if (inviteDomain.Status != InviteStatus.Pending)
            return Result.Failure(new BusinessRuleError("Invite has been accepted or declined"));

        if (inviteDomain.ExpiresAt.HasValue && inviteDomain.ExpiresAt.Value < DateTime.UtcNow)
            return Result.Failure(new BusinessRuleError("Invite has expired"));

        inviteDomain.UpdateInviteStatus(InviteStatus.Declined);
        _context.CrateInvites.Update(inviteDomain.ToEntity());
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<CrateInviteDetailsResponse>> GetInviteDetailsByTokenAsync(string token)
    {
        var inviteEntity = await GetInviteEntityByTokenAsync(token);
        if (inviteEntity == null)
            return Result<CrateInviteDetailsResponse>.Failure(new NotFoundError("Invite not found"));

        if (inviteEntity.Status == InviteStatus.Pending &&
            inviteEntity.ExpiresAt.HasValue &&
            inviteEntity.ExpiresAt.Value < DateTime.UtcNow)
        {
            inviteEntity.Status = InviteStatus.Expired;
            inviteEntity.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Result<CrateInviteDetailsResponse>.Failure(new BusinessRuleError("Invite has expired"));
        }

        var response = new CrateInviteDetailsResponse
        {
            Id = inviteEntity.Id,
            CrateId = inviteEntity.CrateId,
            CrateName = inviteEntity.Crate?.Name ?? "Unknown Crate",
            CrateColor = inviteEntity.Crate?.Color ?? "#6366f1",
            InvitedUserEmail = inviteEntity.InvitedUserEmail,
            Role = inviteEntity.Role,
            Status = inviteEntity.Status,
            Token = inviteEntity.Token,
            ExpiresAt = inviteEntity.ExpiresAt,
        };

        return Result<CrateInviteDetailsResponse>.Success(response);
    }

    private async Task<CrateInviteEntity?> GetInviteEntityByTokenAsync(string token)
    {
        return await _context.CrateInvites.Include(ci => ci.Crate).FirstOrDefaultAsync(i => i.Token == token);
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