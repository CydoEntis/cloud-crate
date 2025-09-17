using CloudCrate.Infrastructure.Identity;
using CloudCrate.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CloudCrate.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public new DatabaseFacade Database => base.Database;
    public DbSet<CrateEntity> Crates { get; set; }
    public DbSet<CrateFileEntity> CrateFiles { get; set; }
    public DbSet<CrateFolderEntity> CrateFolders { get; set; }
    public DbSet<CrateMemberEntity> CrateMembers { get; set; }
    public DbSet<CrateInviteEntity> CrateInvites { get; set; }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Keep your enum conversion - this is useful
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

        // ESSENTIAL: Fix navigation properties for includes to work
        builder.Entity<CrateFileEntity>(b =>
        {
            b.HasQueryFilter(f => !f.IsDeleted);
            b.HasOne(f => f.Crate)
                .WithMany(c => c.Files)
                .HasForeignKey(f => f.CrateId);
        });

        builder.Entity<CrateFolderEntity>(b =>
        {
            b.HasOne(f => f.Crate)
                .WithMany(c => c.Folders)
                .HasForeignKey(f => f.CrateId);
        });

        builder.Entity<CrateMemberEntity>(b =>
        {
            b.HasOne(m => m.Crate)
                .WithMany(c => c.Members)
                .HasForeignKey(m => m.CrateId);

            // Keep this - prevents duplicate members
            b.HasIndex(m => new { m.UserId, m.CrateId }).IsUnique();
        });

        builder.Entity<CrateInviteEntity>(b =>
        {
            b.HasOne(i => i.Crate)
                .WithMany(c => c.Invites)
                .HasForeignKey(i => i.CrateId);
        });

        // ESSENTIAL: Ensure only one root folder per crate
        builder.Entity<CrateFolderEntity>()
            .HasIndex(x => x.CrateId)
            .HasFilter("\"IsRoot\" = TRUE")
            .IsUnique();
    }
}