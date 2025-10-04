using CloudCrate.Domain.Entities;

namespace CloudCrate.Infrastructure.Persistence.Mappers;

public static class ApplicationUserMapper
{
    public static ApplicationUser ToEntity(this UserAccount domain)
    {
        return new ApplicationUser
        {
            Id = domain.Id,
            Email = domain.Email,
            UserName = domain.Email,
            DisplayName = domain.DisplayName,
            ProfilePictureUrl = domain.ProfilePictureUrl,
            Plan = domain.Plan,
            AllocatedStorageBytes = domain.AllocatedStorageBytes,
            UsedStorageBytes = domain.UsedStorageBytes,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.UpdatedAt,
            IsAdmin = domain.IsAdmin,
            IsDemoAccount = domain.IsDemoAccount
        };
    }

    public static UserAccount ToDomain(this ApplicationUser entity)
    {
        return UserAccount.Rehydrate(
            id: entity.Id,
            email: entity.Email!,
            displayName: entity.DisplayName,
            profilePictureUrl: entity.ProfilePictureUrl,
            plan: entity.Plan,
            allocatedStorageBytes: entity.AllocatedStorageBytes,
            usedStorageBytes: entity.UsedStorageBytes,
            createdAt: entity.CreatedAt,
            updatedAt: entity.UpdatedAt,
            isAdmin: entity.IsAdmin,
            isDemoAccount: entity.IsDemoAccount
        );
    }
}