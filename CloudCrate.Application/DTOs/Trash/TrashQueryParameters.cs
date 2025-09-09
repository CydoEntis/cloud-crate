namespace CloudCrate.Application.DTOs.Trash;

public class TrashQueryParameters
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public Guid? CrateId { get; set; } 
    public string? Search { get; set; }
}