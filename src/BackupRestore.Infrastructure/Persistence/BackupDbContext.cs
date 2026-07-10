using BackupRestore.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace BackupRestore.Infrastructure.Persistence;

public class BackupDbContext : DbContext
{
    public BackupDbContext(DbContextOptions<BackupDbContext> options) : base(options)
    {
    }

    public DbSet<BackupJob> BackupJobs => Set<BackupJob>();
    public DbSet<BackupFile> BackupFiles => Set<BackupFile>();
    public DbSet<BackupChunk> BackupChunks => Set<BackupChunk>();
    public DbSet<Checkpoint> Checkpoints => Set<Checkpoint>();
    public DbSet<RestoreJob> RestoreJobs => Set<RestoreJob>();
    public DbSet<JobEvent> JobEvents => Set<JobEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BackupJob>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.BackupId).IsRequired().HasMaxLength(64);
            e.HasIndex(x => x.BackupId).IsUnique();
            e.Property(x => x.BackupName).IsRequired().HasMaxLength(256);
            e.Property(x => x.SourcePath).IsRequired();
            e.Property(x => x.Status).HasConversion<int>();
            e.HasMany(x => x.Files)
                .WithOne(x => x.BackupJob!)
                .HasForeignKey(x => x.BackupJobId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BackupFile>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.BackupId).IsRequired().HasMaxLength(64);
            e.Property(x => x.RelativePath).IsRequired();
            e.Property(x => x.FileHash).HasMaxLength(128);
            e.HasIndex(x => new { x.BackupJobId, x.RelativePath });
            e.HasMany(x => x.Chunks)
                .WithOne(x => x.BackupFile!)
                .HasForeignKey(x => x.BackupFileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BackupChunk>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.VaultPath).IsRequired();
            e.Property(x => x.ChunkHash).HasMaxLength(128);
            e.Property(x => x.Status).HasConversion<int>();
            e.HasIndex(x => new { x.BackupFileId, x.ChunkIndex }).IsUnique();
        });

        modelBuilder.Entity<Checkpoint>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.BackupJobId, x.BackupFileId }).IsUnique();
        });

        modelBuilder.Entity<RestoreJob>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.BackupId).IsRequired().HasMaxLength(64);
            e.Property(x => x.RestorePath).IsRequired();
            e.Property(x => x.Status).HasConversion<int>();
            e.HasIndex(x => x.BackupId);
        });

        modelBuilder.Entity<JobEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.JobType).IsRequired().HasMaxLength(16);
            e.Property(x => x.EventType).IsRequired().HasMaxLength(64);
            e.Property(x => x.Message).IsRequired();
            e.HasIndex(x => x.JobId);
        });
    }
}
