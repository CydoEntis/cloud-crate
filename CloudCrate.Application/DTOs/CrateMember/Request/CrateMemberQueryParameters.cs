using CloudCrate.Application.DTOs.Pagination;
using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.DTOs.CrateMember.Request;

public class CrateMemberQueryParameters : PaginationParameters
{
    public string? SearchTerm { get; set; }
    public CrateMemberSortBy SortBy { get; set; } = CrateMemberSortBy.JoinedAt;
    public bool Ascending { get; set; } = false;
    public CrateRole? FilterByRole { get; set; }
    public bool RecentOnly { get; set; } = false;
    public int? Limit { get; set; }
}