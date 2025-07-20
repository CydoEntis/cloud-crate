using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.DTOs.Roles.Request;

public class AssignRoleRequest
{
    public string UserId { get; set; } = null!;
    public CrateRole Role { get; set; }
}