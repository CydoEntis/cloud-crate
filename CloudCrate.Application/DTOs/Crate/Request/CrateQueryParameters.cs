using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.DTOs.Crate.Request;

public class CrateQueryParameters
{
    public string? UserId { get; set; } = null!;
    public CrateSortBy? SortBy { get; set; } = null;
    public OrderBy OrderBy { get; set; } = OrderBy.Desc;
    public string? SearchTerm { get; set; } = null;
}