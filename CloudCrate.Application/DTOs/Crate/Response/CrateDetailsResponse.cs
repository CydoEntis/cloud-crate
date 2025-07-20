using CloudCrate.Application.DTOs.File.Request;

namespace CloudCrate.Application.DTOs.Crate.Response;

public class CrateDetailsResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = String.Empty;
    public string Color { get; set; } = String.Empty;
    public double TotalUsedStorage { get; set; }
    public double StorageLimit { get; set; }
    public List<FileTypeBreakdownDto> BreakdownByType { get; set; } = [];
    public double RemainingStorage => StorageLimit - TotalUsedStorage;
}