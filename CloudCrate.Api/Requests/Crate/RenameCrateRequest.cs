namespace CloudCrate.Api.Requests.Crate;

public class RenameCrateRequest
{
    public Guid CrateId { get; set; }
    public string NewName { get; set; } = null!;
}