using CloudCrate.Domain.Entities;
using CloudCrate.Infrastructure.Persistence.Entities;

namespace CloudCrate.Infrastructure.Persistence.Mappers;

public static class CrateInviteEntityMapper
{
    public static CrateInviteEntity ToEntity(this CrateInvite invite)
    {
        return new CrateInviteEntity
        {
            Id = invite.Id,
            CrateId = invite.CrateId,
            InvitedUserEmail = invite.InvitedUserEmail,
            InvitedByUserId = invite.InvitedByUserId,
            Role = invite.Role,
            Token = invite.Token,
            CreatedAt = invite.CreatedAt,
            ExpiresAt = invite.ExpiresAt,
            UpdatedAt = invite.UpdatedAt,
            Status = invite.Status
        };
    }


    public static CrateInvite ToDomain(this CrateInviteEntity entity)
    {
        return CrateInvite.Rehydrate(
            entity.Id,
            entity.CrateId,
            entity.InvitedUserEmail,
            entity.InvitedByUserId,
            entity.Role,
            entity.Token,
            entity.CreatedAt,
            entity.ExpiresAt,
            entity.UpdatedAt,
            entity.Status
        );
    }
}