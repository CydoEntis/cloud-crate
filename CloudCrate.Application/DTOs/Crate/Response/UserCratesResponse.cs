namespace CloudCrate.Application.DTOs.Crate.Response;

public class UserCratesResponse
{
    public List<CrateSummaryResponse> Owned { get; set; } = new();
    public List<CrateSummaryResponse> Joined { get; set; } = new();
}