namespace CloudCrate.Application.DTOs.Crate.Response;

public class UserCratesResponse
{
    public List<CrateResponse> Owned { get; set; } = new();
    public List<CrateResponse> Joined { get; set; } = new();
}