using CloudCrate.Domain.Enums;
using CloudCrate.Domain.Exceptions;

namespace CloudCrate.Domain.Entities;

public class Crate
{
    public const long MinAllocationBytes = 1L * 1024 * 1024 * 1024; // 1 GB
    public const string DefaultColor = "#4B9CED";

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Color { get; private set; } = DefaultColor;

    private readonly List<Folder> _folders = new();
    public IReadOnlyCollection<Folder> Folders => _folders.AsReadOnly();

    private readonly List<FileObject> _files = new();
    public IReadOnlyCollection<FileObject> Files => _files.AsReadOnly();

    private readonly List<CrateMember> _members = new();
    public IReadOnlyCollection<CrateMember> Members => _members.AsReadOnly();

    public long AllocatedStorageBytes { get; private set; }
    public long UsedStorageBytes { get; private set; }
    public long RemainingStorageBytes => AllocatedStorageBytes - UsedStorageBytes;

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    protected Crate() { }

    private static long GbToBytes(long gb) => gb * 1024 * 1024 * 1024;

    public static Crate Create(string name, string userId, string? color = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValueEmptyException(nameof(Name));

        if (string.IsNullOrWhiteSpace(userId))
            throw new ValueEmptyException(nameof(userId));

        var crate = new Crate
        {
            Id = Guid.NewGuid(),
            Name = name,
            Color = string.IsNullOrWhiteSpace(color) ? DefaultColor : color,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        crate._members.Add(CrateMember.Create(crate.Id, userId, CrateRole.Owner));

        return crate;
    }

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ValueEmptyException(nameof(Name));

        Name = newName;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetColor(string newColor)
    {
        if (string.IsNullOrWhiteSpace(newColor))
            throw new ValueEmptyException(nameof(Color));

        Color = newColor;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AllocateStorageGB(long gb)
    {
        if (GbToBytes(gb) < MinAllocationBytes)
            throw new MinimumAllocationException(1);

        AllocatedStorageBytes = GbToBytes(gb);
        UpdatedAt = DateTime.UtcNow;
    }

    public void ConsumeStorageBytes(long bytes)
    {
        if (bytes < 0)
            throw new NegativeValueException(nameof(bytes));

        if (UsedStorageBytes + bytes > AllocatedStorageBytes)
            throw new InsufficientStorageException();

        UsedStorageBytes += bytes;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ReleaseStorageBytes(long bytes)
    {
        if (bytes < 0)
            throw new NegativeValueException(nameof(bytes));

        UsedStorageBytes = Math.Max(0, UsedStorageBytes - bytes);
        UpdatedAt = DateTime.UtcNow;
    }

    public string GetCrateStorageName() => $"crate-{Id}".ToLowerInvariant();
}
