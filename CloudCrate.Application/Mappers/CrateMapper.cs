using CloudCrate.Application.DTOs.Crate.Response;
using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.Mappers;

public static class CrateMapper
{
    public static CrateResponse ToCrateResponse(this Domain.Entities.Crate crate, string userId)
    {
        var owner = crate.Members.FirstOrDefault(m => m.Role == CrateRole.Owner);

        var joined = crate.Members.FirstOrDefault(m => m.UserId == userId);

        return new CrateResponse
        {
            Id = crate.Id,
            Name = crate.Name,
            Color = crate.Color,
            Owner = owner is null
                ? null!
                : new CrateMemberResponse
                {
                    UserId = owner.UserId,
                    JoinedAt = owner.JoinedAt,
                    Role = CrateRole.Owner
                },
            UsedStorageBytes = crate.UsedStorage.Bytes,
            TotalStorageBytes = crate.AllocatedStorage.Bytes,
            JoinedAt = joined?.JoinedAt ?? DateTime.MinValue
        };
    }
}