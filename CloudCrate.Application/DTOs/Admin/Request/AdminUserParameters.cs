namespace CloudCrate.Application.DTOs.Admin.Request;

public class AdminUserParameters
{
    public string? SearchTerm { get; set; }
    public string? UserType { get; set; }
    public string? UserStatus { get; set; }
    public string? PlanFilter { get; set; }
    public string? SortBy { get; set; }
    public bool Ascending { get; set; } = false;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}