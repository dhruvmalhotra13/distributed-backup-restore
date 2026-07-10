namespace BackupRestore.Core.Entities;

/// <summary>
/// A recurring backup definition. A scheduler evaluates the cron expression and
/// creates a backup job for the source folder whenever it is due.
/// </summary>
public class BackupSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Backup set name used for the jobs this schedule creates.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Container-visible source path to back up.</summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>Standard 5-field cron expression (e.g. "0 2 * * *" = daily at 02:00 UTC).</summary>
    public string CronExpression { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public DateTime? LastRunAt { get; set; }

    public DateTime? NextRunAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
