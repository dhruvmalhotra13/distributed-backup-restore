namespace BackupRestore.Core.Entities;

/// <summary>
/// An observability event in a job's timeline (state changes, errors, retries).
/// </summary>
public class JobEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid JobId { get; set; }

    /// <summary>"Backup" or "Restore".</summary>
    public string JobType { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
