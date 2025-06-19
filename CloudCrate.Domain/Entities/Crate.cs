using CloudCrate.Infrastructure.Identity;

namespace CloudCrate.Domain.Entities;

public class Crate
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string UserId { get; set; }

    public ICollection<Folder> Folders { get; set; }
    public ICollection<FileObject> Files { get; set; }
}