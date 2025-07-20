namespace CloudCrate.Application.DTOs.File.Request;

public class MoveFileRequest
{
    public Guid? NewParentId { get; set; }
}