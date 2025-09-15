namespace CloudCrate.Application.DTOs.File.Request;

public class BulkDownloadRequest
{
    public List<Guid> FileIds { get; set; } = new();
    public string? ArchiveName { get; set; }
}