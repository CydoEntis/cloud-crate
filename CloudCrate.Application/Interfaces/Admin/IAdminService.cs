using CloudCrate.Application.DTOs.Admin.Request;
using CloudCrate.Application.DTOs.Admin.Response;
using CloudCrate.Application.DTOs.Pagination;
using CloudCrate.Application.Models;
using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.Interfaces.Admin;

public interface IAdminService
{
    Task<Result<bool>> IsUserAdminAsync(string userId);
    Task<Result<PaginatedResult<AdminUserResponse>>> GetUsersAsync(AdminUserParameters parameters);
    Task<Result> BanUserAsync(string adminUserId, string targetUserId);
    Task<Result> UnbanUserAsync(string adminUserId, string targetUserId);
    Task<Result> PromoteToAdminAsync(string adminUserId, string targetUserId);
    Task<Result> RemoveAdminAsync(string adminUserId, string targetUserId);
    Task<Result<AdminStatsResponse>> GetAdminStatsAsync(string adminUserId);
    Task<Result> UpdateUserPlanAsync(string adminUserId, string targetUserId, SubscriptionPlan newPlan);
}