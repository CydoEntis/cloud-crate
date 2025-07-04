using CloudCrate.Application.DTOs.File;

namespace CloudCrate.Application.DTOs.Crate;

public class CrateUsageDto
{
    public double TotalUsedStorage { get; set; }
    public double StorageLimit { get; set; }
    public double RemainingStorage => StorageLimit - TotalUsedStorage;

    public List<FileTypeBreakdownDto> BreakdownByType { get; set; } = new();
}