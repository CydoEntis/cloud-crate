using CloudCrate.Domain.Entities;
using CloudCrate.Infrastructure.Persistence.Entities;

namespace CloudCrate.Infrastructure.Persistence.Mappers;

public static class InviteTokenEntityMapper
{
    public static InviteTokenEntity ToEntity(this InviteToken domain)
    {
        return new InviteTokenEntity
        {
            Id = domain.Id,
            Token = domain.Token,
            CreatedByUserId = domain.CreatedByUserId,
            Email = domain.Email,
            CreatedAt = domain.CreatedAt,
            ExpiresAt = domain.ExpiresAt,
            UsedAt = domain.UsedAt,
            UsedByUserId = domain.UsedByUserId
        };
    }

    public static InviteToken ToDomain(this InviteTokenEntity entity)
    {
        return InviteToken.Rehydrate(
            id: entity.Id,
            token: entity.Token,
            createdByUserId: entity.CreatedByUserId,
            email: entity.Email,
            createdAt: entity.CreatedAt,
            expiresAt: entity.ExpiresAt,
            usedAt: entity.UsedAt,
            usedByUserId: entity.UsedByUserId
        );
    }
}