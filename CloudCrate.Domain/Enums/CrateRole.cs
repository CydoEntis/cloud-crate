public enum CrateRole
{
    Owner, // Full control: delete crate, manage members, all file/folder operations
    Manager, // Manage files/folders, invite/remove users, no crate deletion
    Member, // Upload, create folders, manage own files only
}