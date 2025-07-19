namespace CloudCrate.Application.DTOs.Crate;

public class CrateMemberRequest
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Email { get; set; }
}