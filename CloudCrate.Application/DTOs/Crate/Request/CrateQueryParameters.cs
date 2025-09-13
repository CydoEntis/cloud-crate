using CloudCrate.Application.DTOs.Pagination;
using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.DTOs.Crate.Request;

public class CrateQueryParameters : PaginationParameters
{
    public string? UserId { get; set; } = null!;
    public CrateSortBy? SortBy { get; set; } = null;
    public bool Ascending { get; set; } = false;
    public string? SearchTerm { get; set; } = null;
    public CrateMemberType MemberType { get; set; } = CrateMemberType.All;

}