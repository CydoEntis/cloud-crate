namespace CloudCrate.Application.Interfaces.Permissions;

public interface ICrateRoleService
{
    Task<CrateRole?> GetUserRole(Guid crateId, string userId);
}