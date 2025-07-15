using CloudCrate.Application.Common.Interfaces;
using CloudCrate.Domain.Entities;
using CloudCrate.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CloudCrate.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser>, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DatabaseFacade Database => base.Database;

    public DbSet<Crate> Crates { get; set; }
    public DbSet<FileObject> FileObjects { get; set; }
    public DbSet<FileTag> FileTags { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<Folder> Folders { get; set; }
    public DbSet<Category> Categories { get; set; }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<FileTag>()
            .HasKey(ft => new { ft.FileObjectId, ft.TagId });
    }
}