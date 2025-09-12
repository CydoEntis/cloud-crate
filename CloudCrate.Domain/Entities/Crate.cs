using CloudCrate.Domain.Enums;
using CloudCrate.Domain.Exceptions;
using CloudCrate.Domain.ValueObjects;

namespace CloudCrate.Domain.Entities;

public class Crate
{
    public static readonly StorageSize MinAllocation = StorageSize.FromGigabytes(1);
    public const string DefaultColor = "#4B9CED";

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Color { get; private set; }
    public StorageSize AllocatedStorage { get; private set; }
    public StorageSize UsedStorage { get; private set; }
    public StorageSize RemainingStorage => StorageSize.FromBytes(AllocatedStorage.Bytes - UsedStorage.Bytes);
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<CrateFolder> _folders = new();
    public IReadOnlyCollection<CrateFolder> Folders => _folders.AsReadOnly();

    private readonly List<CrateFile> _files = new();
    public IReadOnlyCollection<CrateFile> Files => _files.AsReadOnly();

    private readonly List<CrateMember> _members = new();
    public IReadOnlyCollection<CrateMember> Members => _members.AsReadOnly();

    private Crate()
    {
    }

    internal Crate(
        Guid id,
        string name,
        string color,
        StorageSize allocatedStorage,
        StorageSize usedStorage,
        DateTime createdAt,
        DateTime updatedAt,
        List<CrateFolder> folders,
        List<CrateFile> files,
        List<CrateMember> members)
    {
        Id = id;
        Name = name;
        Color = color;
        AllocatedStorage = allocatedStorage;
        UsedStorage = usedStorage;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        _folders = folders ?? new List<CrateFolder>();
        _files = files ?? new List<CrateFile>();
        _members = members ?? new List<CrateMember>();
    }

    public static Crate Rehydrate(
        Guid id,
        string name,
        string color,
        StorageSize allocatedStorage,
        StorageSize usedStorage,
        DateTime createdAt,
        DateTime updatedAt,
        List<CrateFolder> folders,
        List<CrateFile> files,
        List<CrateMember> members)
    {
        return new Crate(
            id, name, color, allocatedStorage, usedStorage,
            createdAt, updatedAt, folders, files, members
        );
    }

    public static Crate Create(string name, string userId, long allocatedGb, string? color = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValueEmptyException(nameof(name)); 
        if (string.IsNullOrWhiteSpace(userId))
            throw new ValueEmptyException(nameof(userId));
        if (allocatedGb < 1) 
            throw new MinimumAllocationException(1);

        var crate = new Crate
        {
            Id = Guid.NewGuid(),
            Name = name,
            Color = string.IsNullOrWhiteSpace(color) ? DefaultColor : color,
            AllocatedStorage = StorageSize.FromGigabytes(allocatedGb),
            UsedStorage = StorageSize.FromBytes(0),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        crate._members.Add(CrateMember.Create(crate.Id, userId, CrateRole.Owner));

        var rootFolder = CrateFolder.CreateRoot("Root", crate.Id, null, null, userId);
        crate._folders.Add(rootFolder);

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

    public void AllocateStorage(long gb)
    {
        var newSize = StorageSize.FromGigabytes(gb);

        if (newSize.Bytes < MinAllocation.Bytes)
            throw new MinimumAllocationException(1);
        if (UsedStorage.Bytes > newSize.Bytes)
            throw new InvalidOperationException("Cannot allocate less than already used storage.");

        AllocatedStorage = newSize;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool TryAllocateStorage(long gb, out string? error)
    {
        error = null;
        var newSize = StorageSize.FromGigabytes(gb);

        if (newSize.Bytes < MinAllocation.Bytes)
        {
            error = "Minimum allocation is 1 GB.";
            return false;
        }

        if (UsedStorage.Bytes > newSize.Bytes)
        {
            error = "Cannot allocate less than already used storage.";
            return false;
        }

        AllocatedStorage = newSize;
        UpdatedAt = DateTime.UtcNow;
        return true;
    }

    public void ConsumeStorage(StorageSize size)
    {
        if (size.Bytes < 0)
            throw new NegativeValueException(nameof(size));
        if (UsedStorage.Bytes + size.Bytes > AllocatedStorage.Bytes)
            throw new InsufficientStorageException();

        UsedStorage = StorageSize.FromBytes(UsedStorage.Bytes + size.Bytes);
        UpdatedAt = DateTime.UtcNow;
    }

    public void ReleaseStorage(StorageSize size)
    {
        if (size.Bytes < 0)
            throw new NegativeValueException(nameof(size));

        UsedStorage = StorageSize.FromBytes(Math.Max(0, UsedStorage.Bytes - size.Bytes));
        UpdatedAt = DateTime.UtcNow;
    }

    public string GetCrateStorageName() => $"crate-{Id}".ToLowerInvariant();
}