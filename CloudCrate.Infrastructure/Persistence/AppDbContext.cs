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

        builder.Entity<CrateFolderEntity>(b =>
        {
            b.HasIndex(x => new { x.CrateId })
                .HasFilter("\"IsRoot\" = TRUE")
                .IsUnique();

            b.Property(x => x.IsRoot).HasDefaultValue(false);

            b.HasOne(f => f.CreatedByUser)
                .WithMany()
                .HasForeignKey(f => f.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(f => f.DeletedByUser)
                .WithMany()
                .HasForeignKey(f => f.DeletedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(f => f.RestoredByUser)
                .WithMany()
                .HasForeignKey(f => f.RestoredByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(f => f.ParentFolder)
                .WithMany(f => f.Subfolders)
                .HasForeignKey(f => f.ParentFolderId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(f => f.Crate)
                .WithMany()
                .HasForeignKey(f => f.CrateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CrateFileEntity>(b =>
        {
            b.HasQueryFilter(f => !f.IsDeleted);

            b.HasOne(f => f.UploadedByUser)
                .WithMany()
                .HasForeignKey(f => f.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(f => f.DeletedByUser)
                .WithMany()
                .HasForeignKey(f => f.DeletedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(f => f.RestoredByUser)
                .WithMany()
                .HasForeignKey(f => f.RestoredByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(f => f.Crate)
                .WithMany()
                .HasForeignKey(f => f.CrateId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(f => f.CrateFolder)
                .WithMany(f => f.Files)
                .HasForeignKey(f => f.CrateFolderId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<CrateMemberEntity>(b =>
        {
            b.HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(m => m.Crate)
                .WithMany()
                .HasForeignKey(m => m.CrateId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(m => new { m.UserId, m.CrateId }).IsUnique();
        });

        builder.Entity<CrateInviteEntity>(b =>
        {
            b.HasOne(i => i.InvitedByUser)
                .WithMany()
                .HasForeignKey(i => i.InvitedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(i => i.Crate)
                .WithMany()
                .HasForeignKey(i => i.CrateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CrateEntity>(b =>
        {
            b.Property(c => c.AllocatedStorageBytes).IsRequired();
            b.Property(c => c.UsedStorageBytes).IsRequired();
        });
    }
}