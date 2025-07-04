namespace CloudCrate.Application.DTOs.Crate;

public class CrateUsageDto
{
    public int TotalUsed { get; set; }
    public int StorageLimit { get; set; }
    public int Remaining => StorageLimit - TotalUsed;
    public Dictionary<string, int> BreakdownByType { get; set; } = new();
}