using CloudCrate.Application.DTOs.File;
using CloudCrate.Application.DTOs.File.Response;

namespace CloudCrate.Application.DTOs.Folder.Response;

public class FolderContentsResponse
{
    public List<CrateFolderResponse> Folders { get; set; } = new();
    public List<CrateFileResponse> Files { get; set; } = new();
    public string FolderName { get; set; } = string.Empty;
    public Guid? ParentFolderId { get; set; }
    public List<FolderBreadcrumb> Breadcrumbs { get; set; } = new();
    public int TotalFolders { get; set; }
    public int TotalFiles { get; set; }
}