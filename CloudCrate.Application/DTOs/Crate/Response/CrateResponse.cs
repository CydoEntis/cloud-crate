using CloudCrate.Domain.Entities;
using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.DTOs.Crate.Response;

public class CrateResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public CrateMemberResponse Owner { get; set; } = null!;
    public long UsedStorage { get; set; }
    public DateTime JoinedAt { get; set; }
}