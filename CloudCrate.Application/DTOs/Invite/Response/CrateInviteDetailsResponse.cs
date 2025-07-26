using CloudCrate.Domain.Entities;
using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.DTOs.Invite.Response;

public class CrateInviteDetailsResponse
{
    public Guid Id { get; set; }
    public Guid CrateId { get; private set; }
    public string CrateName { get; set; }
    public string CrateColor { get; set; }
    public string InvitedUserEmail { get; set; }
    public CrateRole Role { get; set; }
    public string Token { get; set; }
    public DateTime? ExpiresAt { get; set; }


    public static CrateInviteDetailsResponse FromEntity(CrateInvite entity)
    {
        return new CrateInviteDetailsResponse
        {
            Id = entity.Id,
            CrateId = entity.CrateId,
            CrateName = entity.Crate?.Name ?? string.Empty,
            CrateColor = entity.Crate?.Color ?? string.Empty,
            InvitedUserEmail = entity.InvitedUserEmail,
            Token = entity.Token,
            Role = entity.Role,
            ExpiresAt = entity.ExpiresAt
        };
    }
}