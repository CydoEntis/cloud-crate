namespace CloudCrate.Application.DTOs.File.Request;

public class DownloadFileRequest
{
    public Guid CrateId { get; set; }
    public Guid FileId { get; set; }
}