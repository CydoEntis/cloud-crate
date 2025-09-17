using CloudCrate.Application.DTOs.File.Request;
using CloudCrate.Application.DTOs.File.Response;
using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.DTOs.Crate.Response;

public class CrateDetailsResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public long AllocatedStorageBytes { get; set; }
    public long UsedStorageBytes { get; set; }
    public long RemainingStorageBytes => AllocatedStorageBytes - UsedStorageBytes;
    public CrateRole Role { get; set; }
    public List<FileTypeBreakdownResponse> BreakdownByType { get; set; } = [];

    public Guid RootFolderId { get; set; }
}