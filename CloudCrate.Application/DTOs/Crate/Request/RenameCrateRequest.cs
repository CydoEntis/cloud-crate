namespace CloudCrate.Application.DTOs.Crate.Request;

public class RenameCrateRequest
{
    public Guid CrateId { get; set; }
    public string NewName { get; set; } = null!;
}