namespace CloudCrate.Application.DTOs.Folder.Request;

public class GetAvailableMoveTargetsRequest
{
    public Guid CrateId { get; set; }
    public Guid? ExcludeFolderId { get; set; }
    public string? SearchTerm { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}