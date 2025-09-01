namespace CloudCrate.Application.DTOs.Crate.Request;

public class CrateMemberRequest
{
    public string? Email { get; set; }  
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string UserId { get; set; }
    
}