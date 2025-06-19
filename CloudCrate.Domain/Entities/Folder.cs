using CloudCrate.Domain.Entities;

namespace CloudCrate.Infrastructure.Identity;

public class Folder
{
    public Guid Id { get; set; }
    public string Name { get; set; }

    public Guid? ParentFolderId { get; set; }
    public Folder? ParentFolder { get; set; }

    public Guid CrateId { get; set; }
    public Crate Crate { get; set; }

    public ICollection<FileObject> Files { get; set; }
    public ICollection<Folder> Subfolders { get; set; }
}