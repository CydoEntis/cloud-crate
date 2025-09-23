using CloudCrate.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CloudCrate.Infrastructure.Persistence.Configurations;

public class InviteTokenEntityConfiguration : IEntityTypeConfiguration<InviteTokenEntity>
{
    public void Configure(EntityTypeBuilder<InviteTokenEntity> builder)
    {
        builder.ToTable("InviteTokens");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasMaxLength(36)
            .IsRequired();

        builder.Property(x => x.Token)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.CreatedByUserId)
            .HasMaxLength(36)
            .IsRequired();

        builder.Property(x => x.Email)
            .HasMaxLength(256)
            .IsRequired(false);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.ExpiresAt)
            .IsRequired();

        builder.Property(x => x.UsedAt)
            .IsRequired(false);

        builder.Property(x => x.UsedByUserId)
            .HasMaxLength(36)
            .IsRequired(false);

        builder.HasIndex(x => x.Token)
            .IsUnique()
            .HasDatabaseName("IX_InviteTokens_Token");

        builder.HasIndex(x => x.CreatedByUserId)
            .HasDatabaseName("IX_InviteTokens_CreatedByUserId");

        builder.HasIndex(x => x.ExpiresAt)
            .HasDatabaseName("IX_InviteTokens_ExpiresAt");

        builder.HasIndex(x => x.UsedAt)
            .HasDatabaseName("IX_InviteTokens_UsedAt");

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.UsedByUser)
            .WithMany()
            .HasForeignKey(x => x.UsedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}