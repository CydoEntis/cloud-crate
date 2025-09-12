using CloudCrate.Domain.Entities;
using CloudCrate.Infrastructure.Persistence.Entities;

namespace CloudCrate.Infrastructure.Persistence.Mappers;

public static class CrateMemberMapper
{
    public static CrateMemberEntity ToEntity(this CrateMember member, Guid crateId)
    {
        return new CrateMemberEntity
        {
            Id = member.Id,
            CrateId = crateId,
            UserId = member.UserId,
            Role = member.Role,
            JoinedAt = member.JoinedAt,
            UpdatedAt = member.UpdatedAt
        };
    }

    public static CrateMember ToDomain(this CrateMemberEntity entity)
    {
        return CrateMember.Rehydrate(
            entity.Id,
            entity.CrateId,
            entity.UserId,
            entity.Role,
            entity.JoinedAt,
            entity.UpdatedAt
        );
    }
}