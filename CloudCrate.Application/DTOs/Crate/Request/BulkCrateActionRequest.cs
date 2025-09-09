namespace CloudCrate.Application.DTOs.Crate.Request;

public class BulkCrateActionRequest
{
    public List<Guid> CrateIds { get; set; } = new();
}