namespace CloudCrate.Application.DTOs.File;

public class FileTypeBreakdownDto
{
    public string Type { get; set; } = default!;
    public int SizeMb { get; set; }
}