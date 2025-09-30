namespace CloudCrate.Application.DTOs.Trash;

public class TrashQueryParameters
{
    public Guid CrateId { get; set; }
    public string UserId { get; set; } = string.Empty;

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    public string? SearchTerm { get; set; }

    public TrashSortBy SortBy { get; set; } = TrashSortBy.DeletedAt;
    public bool Ascending { get; set; } = false;
}

public enum TrashSortBy
{
    Name,
    DeletedAt,
    Size
}