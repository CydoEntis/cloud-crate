namespace CloudCrate.Domain.Enums;

using System.Text.Json.Serialization;

public enum CrateRole
{
    Owner,        // Full control: folders, files, permissions
    Contributor,  // Upload, move, view, edit files/folders, create folders
    Uploader,     // Upload and view files only
    Downloader,   // View and download only
    Viewer,       // View only
}