namespace CloudCrate.Application.DTOs.File.Response;

public class FileDownloadResult
{
    public byte[] Content { get; init; } = null!;
    public string FileName { get; init; } = null!;
    public string MimeType { get; init; } = null!;
    public long SizeInBytes { get; init; }
}