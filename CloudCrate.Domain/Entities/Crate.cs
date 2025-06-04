namespace CloudCrate.Domain.Entities;

public class Crate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string OwnerId { get; set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<FileObject> _files = new();
    public IReadOnlyCollection<FileObject> Files => _files.AsReadOnly();

    private Crate()
    {
    }

    private Crate(string name, string ownerId)
    {
        Id = Guid.NewGuid();
        Name = name;
        OwnerId = ownerId;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }


    public static Crate Create(string name, string ownerId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Crate name cannot be empty.");

        if (string.IsNullOrWhiteSpace(ownerId))
            throw new ArgumentException("Owner ID is required.");

        return new Crate(name, ownerId);
    }

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("New name cannot be empty.");

        Name = newName;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddFile(FileObject file)
    {
        if (file == null)
            throw new ArgumentNullException(nameof(file));

        _files.Add(file);
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveFile(Guid fileId)
    {
        var file = _files.FirstOrDefault(f => f.Id == fileId);
        if (file == null)
            throw new InvalidOperationException("File not found in this crate.");

        _files.Remove(file);
        UpdatedAt = DateTime.UtcNow;
    }
}