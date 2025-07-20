using CloudCrate.Application.Common.Models;
using CloudCrate.Domain.Entities;
using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.Interfaces.Crate;

public interface ICrateInviteService
{
    Task<Result> CreateInviteAsync(Guid crateId, string invitedEmail, string invitedByUserId,
        CrateRole role, DateTime? expiresAt = null);

    Task<Result<CrateInvite?>> GetInviteByTokenAsync(string token);

    Task<Result> AcceptInviteAsync(string token, string userId, ICrateUserRoleService roleService);

    Task<Result> DeclineInviteAsync(string token);
}