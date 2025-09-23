using CloudCrate.Application.DTOs.Invite.Request;
using CloudCrate.Application.DTOs.Invite.Response;
using CloudCrate.Application.Models;
using CloudCrate.Domain.Entities;

namespace CloudCrate.Application.Interfaces.Invite;

public interface IUserInviteService
{
    Task<Result<InviteUserResponse>> CreateInviteAsync(string createdByUserId, CreateUserInviteRequest request);
    Task<Result<InviteToken>> ValidateInviteTokenAsync(string token);
    Task<Result> MarkInviteAsUsedAsync(string token, string usedByUserId);
    Task<Result<IEnumerable<InviteUserResponse>>> GetInvitesByUserAsync(string userId);
    Task<Result> DeleteExpiredInvitesAsync();
}