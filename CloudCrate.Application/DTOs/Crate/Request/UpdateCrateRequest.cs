namespace CloudCrate.Application.DTOs.Crate.Request;

public class UpdateCrateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public int StorageAllocationGb { get; set; }
}