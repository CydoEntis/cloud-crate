using CloudCrate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using CrateEntity = CloudCrate.Domain.Entities.Crate;

namespace CloudCrate.Application.Interfaces.Persistence;

public interface IAppDbContext
{
    DatabaseFacade Database { get; }
    DbSet<CrateEntity> Crates { get; }
    DbSet<CrateFile> CrateFiles { get; }
    DbSet<CrateFolder> CrateFolders { get; }
    DbSet<CrateMember> CrateMembers { get; }
    public DbSet<CrateInvite> CrateInvites { get; set; }

    DbSet<FileObject> FileObjects { get; }
    DbSet<Domain.Entities.Folder> Folders { get; }


    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}