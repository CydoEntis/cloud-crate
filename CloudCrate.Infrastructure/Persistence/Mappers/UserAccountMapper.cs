using CloudCrate.Domain.Entities;
using CloudCrate.Infrastructure.Identity;

namespace CloudCrate.Infrastructure.Persistence.Mappers;

public static class UserAccountMapper
{
    public static ApplicationUser ToEntity(this UserAccount user)
    {
        return new ApplicationUser
        {
            Id = user.Id,
            UserName = user.Email,
            NormalizedUserName = user.Email.ToUpperInvariant(),
            Email = user.Email,
            NormalizedEmail = user.Email.ToUpperInvariant(),
            DisplayName = user.DisplayName,
            ProfilePictureUrl = user.ProfilePictureUrl,
            Plan = user.Plan,
            UsedAccountStorageBytes = user.UsedStorageBytes,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    public static UserAccount ToDomain(this ApplicationUser entity)
    {
        return UserAccount.Rehydrate(
            entity.Id,
            entity.Email,
            entity.DisplayName,
            entity.ProfilePictureUrl,
            entity.Plan,
            entity.UsedAccountStorageBytes,
            entity.CreatedAt,
            entity.UpdatedAt
        );
    }
}