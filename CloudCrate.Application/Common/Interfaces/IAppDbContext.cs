using CloudCrate.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CloudCrate.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<Crate> Crates { get; }
    DbSet<FileObject> FileObjects { get; }
    DbSet<Folder> Folders { get; }
    DbSet<Tag> Tags { get; }
    DbSet<FileTag> FileTags { get; }
    DbSet<Category> Categories { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}