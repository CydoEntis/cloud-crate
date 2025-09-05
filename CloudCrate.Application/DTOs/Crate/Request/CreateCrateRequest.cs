namespace CloudCrate.Application.DTOs.Crate.Request;

public class CreateCrateRequest
{
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public int AllocatedStorageGb { get; set; } = 1;
    public string? UserId { get; set; }
}