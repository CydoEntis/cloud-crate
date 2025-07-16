namespace CloudCrate.Domain.Enums;

public enum CrateRole
{
    Owner,      // Full control, including deleting the crate
    Editor,     // Can upload/download/delete files
    Viewer      // Read-only access
}