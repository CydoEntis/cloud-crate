namespace CloudCrate.Application.DTOs.Crate.Response;

public class CrateResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
}