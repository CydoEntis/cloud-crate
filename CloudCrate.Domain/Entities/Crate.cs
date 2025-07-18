namespace CloudCrate.Domain.Entities;

public class Crate
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string UserId { get; set; }
    public string Color { get; set; }
    public ICollection<Folder> Folders { get; set; }
    public ICollection<FileObject> Files { get; set; }
    public ICollection<CrateUserRole> Permissions { get; set; }
    protected Crate()
    {
    }

    public static Crate Create(string name, string userId, string? color = null)
    {
        return new Crate
        {
            Id = Guid.NewGuid(),
            Name = name,
            UserId = userId,
            Color = color ?? "#4B9CED",
            Folders = new List<Folder>(),
            Files = new List<FileObject>(),
            Permissions = new List<CrateUserRole>()
        };
    }

    public void Rename(string newName)
    {
        Name = newName;
    }

    public void SetColor(string newColor)
    {
        Color = newColor;
    }
}