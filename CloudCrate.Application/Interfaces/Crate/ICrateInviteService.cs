using CloudCrate.Application.DTOs.Invite.Request;
using CloudCrate.Application.Models;

namespace CloudCrate.Application.Interfaces.Crate;

public interface ICrateInviteService
{
    Task<Result> CreateInviteAsync(CrateInviteRequest request);

    Task<Result> AcceptInviteAsync(string token, string userId);

    Task<Result> DeclineInviteAsync(string token);
}