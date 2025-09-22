namespace CloudCrate.Application.DTOs.Admin.Request;

public class AdminUserParameters
{
    public string? SearchTerm { get; set; }
    public bool? IsAdmin { get; set; }
    public bool? IsLocked { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    public string? Plan { get; set; }
    public AdminOrderBy OrderBy { get; set; } = AdminOrderBy.CreatedAt;
    public bool Ascending { get; set; } = false;
    public bool IncludeDeleted { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}