using CloudCrate.Domain.Enums;

namespace CloudCrate.Domain.Permissions;

public static class CrateRolePermissions
{
    public static bool CanUpload(CrateRole role) =>
        role == CrateRole.Owner || role == CrateRole.Editor;

    public static bool CanDownload(CrateRole role) =>
        role == CrateRole.Owner || role == CrateRole.Editor || role == CrateRole.Viewer;

    public static bool CanDeleteFiles(CrateRole role) =>
        role == CrateRole.Owner || role == CrateRole.Editor;

    public static bool CanEditCrate(CrateRole role) =>
        role == CrateRole.Owner;

    public static bool CanManagePermissions(CrateRole role) =>
        role == CrateRole.Owner;
}