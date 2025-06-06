using CloudCrate.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CloudCrate.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<Crate> Crates { get; }
    DbSet<FileObject> FileObjects { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}