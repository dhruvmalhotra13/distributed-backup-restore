using BackupRestore.Core.Enums;

namespace BackupRestore.Core.Entities;

/// <summary>
/// A restore job: reconstructs a backup version into a target folder.
/// </summary>
public class RestoreJob
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The vault backup identifier being restored.</summary>
    public string BackupId { get; set; } = string.Empty;

    public string RestorePath { get; set; } = string.Empty;

    public JobStatus Status { get; set; } = JobStatus.Queued;

    public long TotalBytes { get; set; }

    public long RestoredBytes { get; set; }

    public double ProgressPercent { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
