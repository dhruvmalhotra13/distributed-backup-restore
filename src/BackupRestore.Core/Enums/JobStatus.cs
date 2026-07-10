namespace BackupRestore.Core.Enums;

/// <summary>
/// Lifecycle status shared by backup and restore jobs.
/// </summary>
public enum JobStatus
{
    Queued = 0,
    Running = 1,
    Paused = 2,
    Cancelled = 3,
    Completed = 4,
    Failed = 5,
    PartiallyFailed = 6
}
