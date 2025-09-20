using CloudCrate.Application.DTOs.Invite.Request;
using CloudCrate.Application.DTOs.Invite.Response;
using CloudCrate.Application.Models;

namespace CloudCrate.Application.Interfaces.Crate;

public interface ICrateInviteService
{
    Task<Result<CrateInviteDetailsResponse>> GetInviteDetailsByTokenAsync(string token);
    Task<Result> CreateInviteAsync(string userId, CrateInviteRequest request);

    Task<Result> AcceptInviteAsync(string token, string userId);

    Task<Result> DeclineInviteAsync(string token);
}