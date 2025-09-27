namespace CloudCrate.Application.DTOs.File.Request;

public class UpdateFileRequest
{
    public Guid FileId { get; set; }
    public string? NewName { get; set; }
}