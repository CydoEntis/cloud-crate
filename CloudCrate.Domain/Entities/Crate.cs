namespace CloudCrate.Domain.Entities;

public class Crate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string OwnerId { get; set; } = null!;
    public List<FileObject> Files { get; set; } = [];
}