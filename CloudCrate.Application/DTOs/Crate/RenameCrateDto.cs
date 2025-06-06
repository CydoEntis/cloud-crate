namespace CloudCrate.Application.DTOs.Crate;

public class RenameCrateDto
{
    public Guid CrateId { get; set; }
    public string NewName { get; set; } = null!;
}