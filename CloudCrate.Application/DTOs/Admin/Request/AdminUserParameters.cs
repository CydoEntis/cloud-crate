using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.DTOs.Admin.Request;

public class AdminUserParameters
{
    public string? SearchTerm { get; set; }
    public AdminUserType UserType { get; set; } = AdminUserType.All;
    public AdminUserStatus UserStatus { get; set; } = AdminUserStatus.All;
    public AdminPlanFilter PlanFilter { get; set; } = AdminPlanFilter.All;
    public AdminUserSortBy SortBy { get; set; } = AdminUserSortBy.CreatedAt;
    public bool Ascending { get; set; } = false;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}