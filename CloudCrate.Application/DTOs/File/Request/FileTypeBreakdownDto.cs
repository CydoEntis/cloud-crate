namespace CloudCrate.Application.DTOs.File.Request;

public class FileTypeBreakdownDto
{
    public string Type { get; set; } = default!;
    public double SizeMb { get; set; }
}