﻿using CloudCrate.Domain.Enums;

namespace CloudCrate.Domain.Entities;

public class Crate
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Color { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public ICollection<Folder> Folders { get; set; }
    public ICollection<FileObject> Files { get; set; }
    public ICollection<CrateMember> Members { get; set; }

    protected Crate()
    {
    }

    public static Crate Create(string name, string userId, string? color = null)
    {
        var crate = new Crate
        {
            Id = Guid.NewGuid(),
            Name = name,
            Color = color ?? "#4B9CED",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Folders = new List<Folder>(),
            Files = new List<FileObject>(),
            Members = new List<CrateMember>()
        };

        crate.Members.Add(CrateMember.Create(crate.Id, userId, CrateRole.Owner));

        return crate;
    }

    public void Rename(string newName)
    {
        Name = newName;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetColor(string newColor)
    {
        Color = newColor;
        UpdatedAt = DateTime.UtcNow;
    }

    public string GetCrateStorageName()
    {
        return $"crate-{Id}".ToLowerInvariant();
    }
}