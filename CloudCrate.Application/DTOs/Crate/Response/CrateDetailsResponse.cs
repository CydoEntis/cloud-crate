using CloudCrate.Application.DTOs.File.Request;
using CloudCrate.Application.DTOs.File.Response;
using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.DTOs.Crate.Response;

public class CrateDetailsResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public CrateRole Role { get; set; }
    public string Color { get; set; } = string.Empty;
    public long TotalUsedStorage { get; set; }
    public long StorageLimit { get; set; }
    public List<FileTypeBreakdownResponse> BreakdownByType { get; set; } = [];
    public long RemainingStorage => StorageLimit - TotalUsedStorage;

    public Guid RootFolderId { get; set; }
}