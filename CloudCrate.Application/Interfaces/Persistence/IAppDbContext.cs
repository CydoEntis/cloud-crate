using CloudCrate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CloudCrate.Application.Interfaces.Persistence;

public interface IAppDbContext
{
    DatabaseFacade Database { get; }
    DbSet<Domain.Entities.Crate> Crates { get; }
    DbSet<CrateMember> CrateMembers { get; }
    public DbSet<CrateInvite> CrateInvites { get; set; }

    DbSet<FileObject> FileObjects { get; }
    DbSet<Domain.Entities.Folder> Folders { get; }


    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}