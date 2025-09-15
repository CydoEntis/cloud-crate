namespace CloudCrate.Application.DTOs.File.Request;

public class BulkOperationRequest
{
    public List<Guid> FileIds { get; set; } = new();
}