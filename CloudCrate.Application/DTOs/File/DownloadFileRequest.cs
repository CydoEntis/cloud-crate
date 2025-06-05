namespace CloudCrate.Application.DTOs.File;

public class DownloadFileRequest
{
    public Guid CrateId { get; set; }
    public Guid FileId { get; set; }
}