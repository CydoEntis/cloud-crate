using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.DTOs.Crate.Request;

public class CrateQueryParameters
{
    public string? UserId { get; set; } = null!;
    public CrateSortBy? SortBy { get; set; } = null;
    public OrderBy OrderBy { get; set; } = OrderBy.Desc;
    public string? SearchTerm { get; set; } = null;
    public bool? OwnerOnly { get; set; } = null;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}