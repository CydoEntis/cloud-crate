namespace CloudCrate.Application.DTOs.Admin.Response;

public class AdminStatsResponse
{
    public int TotalUsers { get; set; }
    public int AdminUsers { get; set; }
    public int LockedUsers { get; set; }
    public int ActiveUsers { get; set; }
    public long TotalStorageUsed { get; set; }
    public DateTime? LastUserRegistered { get; set; }
    public Dictionary<string, int> UsersByPlan { get; set; } = new();
}