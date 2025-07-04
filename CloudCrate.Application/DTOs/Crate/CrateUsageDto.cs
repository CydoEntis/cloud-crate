using CloudCrate.Application.DTOs.File;

namespace CloudCrate.Application.DTOs.Crate;

public class CrateUsageDto
{
    public int TotalUsedStorage { get; set; }
    public int StorageLimit { get; set; }
    public int RemainingStorage => StorageLimit - TotalUsedStorage;

    public List<FileTypeBreakdownDto> BreakdownByType { get; set; } = new();
}