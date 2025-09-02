namespace CloudCrate.Application.DTOs.Crate.Request;

public class CreateCrateRequest
{
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public double RequestedAllocationGb { get; set; } = 1;
}