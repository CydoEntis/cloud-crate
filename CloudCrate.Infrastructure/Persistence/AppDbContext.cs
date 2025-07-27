using CloudCrate.Application.Interfaces.Persistence;
using CloudCrate.Domain.Entities;
using CloudCrate.Domain.Enums;
using CloudCrate.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CloudCrate.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser>, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DatabaseFacade Database => base.Database;
    public DbSet<Crate> Crates { get; set; }
    public DbSet<CrateMember> CrateMembers { get; set; }
    public DbSet<CrateInvite> CrateInvites { get; set; }
    public DbSet<FileObject> FileObjects { get; set; }
    public DbSet<Folder> Folders { get; set; }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.ClrType.GetProperties())
            {
                if (property.PropertyType.IsEnum)
                {
                    builder
                        .Entity(entityType.ClrType)
                        .Property(property.Name)
                        .HasConversion<string>();
                }
            }
        }

        base.OnModelCreating(builder);
    }
}