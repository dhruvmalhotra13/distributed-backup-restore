using BackupRestore.Core.Enums;

namespace BackupRestore.Core.Entities;

/// <summary>
/// A backup job: one request to back up a source folder into the Backup Vault.
/// </summary>
public class BackupJob
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Human-friendly vault identifier, e.g. "backup-a1b2c3d4".</summary>
    public string BackupId { get; set; } = string.Empty;

    public string BackupName { get; set; } = string.Empty;

    public string SourcePath { get; set; } = string.Empty;

    public JobStatus Status { get; set; } = JobStatus.Queued;

    /// <summary>Version number within a backup set (same BackupName). Starts at 1.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Bytes that did NOT need to be stored thanks to incremental/dedup reuse.</summary>
    public long DedupedBytes { get; set; }

    public long TotalBytes { get; set; }

    public long CopiedBytes { get; set; }

    public int TotalFiles { get; set; }

    public int FilesProcessed { get; set; }

    public double ProgressPercent { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<BackupFile> Files { get; set; } = new();
}
