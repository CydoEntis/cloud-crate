namespace CloudCrate.Application.DTOs.Folder;

public class FolderDownloadResult
{
    public byte[] FileBytes { get; set; } = Array.Empty<byte>();
    public string FileName { get; set; } = "downloaded-folder";
}