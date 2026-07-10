namespace BackupRestore.Core.Contracts;

/// <summary>
/// Command published when a restore job should be processed by a worker.
/// </summary>
public record RestoreRequested
{
    public Guid RestoreJobId { get; init; }
    public string BackupId { get; init; } = string.Empty;
    public string RestorePath { get; init; } = string.Empty;
}
