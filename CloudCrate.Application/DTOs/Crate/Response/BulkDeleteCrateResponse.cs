namespace CloudCrate.Application.DTOs.Crate.Response;

public class BulkDeleteCrateResponse
{
    public int DeletedCount { get; init; }
    public int RequestedCount { get; init; }
    public List<Guid> DeletedCrateIds { get; init; } = new();
    public List<Guid> SkippedCrateIds { get; init; } = new();
    public string Message { get; init; } = string.Empty;
}