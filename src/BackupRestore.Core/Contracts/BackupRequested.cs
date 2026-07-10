namespace BackupRestore.Core.Contracts;

/// <summary>
/// Command published when a backup job should be processed by a worker.
/// </summary>
public record BackupRequested
{
    public Guid JobId { get; init; }
    public string BackupId { get; init; } = string.Empty;
    public string BackupName { get; init; } = string.Empty;
    public string SourcePath { get; init; } = string.Empty;
    public int ChunkSizeBytes { get; init; }
}
