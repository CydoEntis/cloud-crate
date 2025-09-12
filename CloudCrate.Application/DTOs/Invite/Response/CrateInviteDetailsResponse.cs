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


    public static CrateInviteDetailsResponse FromDomain(CrateInvite domain)
    {
        return new CrateInviteDetailsResponse
        {
            Id = domain.Id,
            CrateId = domain.CrateId,
            CrateName = domain.Crate?.Name ?? string.Empty,
            CrateColor = domain.Crate?.Color ?? string.Empty,
            InvitedUserEmail = domain.InvitedUserEmail,
            Token = domain.Token,
            Role = domain.Role,
            ExpiresAt = domain.ExpiresAt
        };
    }
}