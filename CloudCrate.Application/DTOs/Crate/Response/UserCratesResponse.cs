namespace CloudCrate.Application.DTOs.Crate.Response;

public class UserCratesResponse
{
    public List<CrateListItemResponse> Owned { get; set; } = new();
    public List<CrateListItemResponse> Joined { get; set; } = new();
}