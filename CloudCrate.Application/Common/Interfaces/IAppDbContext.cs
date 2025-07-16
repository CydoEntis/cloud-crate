using CloudCrate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CloudCrate.Application.Common.Interfaces;

public interface IAppDbContext
{
    DatabaseFacade Database { get; }
    DbSet<Crate> Crates { get; }
    DbSet<CrateUserRole> CrateUserRoles { get; }
    public DbSet<CrateInvite> CrateInvites { get; set; }

    DbSet<FileObject> FileObjects { get; }
    DbSet<Folder> Folders { get; }
    

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}