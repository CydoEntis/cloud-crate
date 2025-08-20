using CloudCrate.Application.DTOs.File.Response;
using CloudCrate.Application.DTOs.Pagination;

namespace CloudCrate.Application.DTOs.Folder.Response;

public class FolderContentsResponse
{
    public IEnumerable<FolderResponse> Folders { get; set; } = new List<FolderResponse>();
    public PaginatedResult<FileObjectResponse> Files { get; set; } = new PaginatedResult<FileObjectResponse>();
}
