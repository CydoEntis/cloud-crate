using CloudCrate.Domain.Entities;
using CloudCrate.Infrastructure.Persistence.Entities;

namespace CloudCrate.Infrastructure.Persistence.Mappers;

public static class CrateMemberEntityMapper
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
        var member = CrateMember.Rehydrate(
            entity.Id,
            entity.CrateId,
            entity.UserId,
            entity.Role,
            entity.JoinedAt,
            entity.UpdatedAt
        );

        // Use ApplicationUserMapper to map EF User -> Domain UserAccount
        if (entity.User != null)
        {
            member.User = entity.User.ToDomain(); // <-- reuse the mapper
        }

        return member;
    }
}