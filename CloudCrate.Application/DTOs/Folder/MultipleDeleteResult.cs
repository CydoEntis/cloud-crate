namespace CloudCrate.Application.DTOs.Folder;

public class MultipleDeleteResult
{
    public List<Guid> DeletedFiles { get; set; } = new();
    public List<Guid> SkippedFiles { get; set; } = new();
    public List<Guid> FailedFiles { get; set; } = new();

    public List<Guid> DeletedFolders { get; set; } = new();
    public List<Guid> SkippedFolders { get; set; } = new();
    public List<Guid> FailedFolders { get; set; } = new();
}