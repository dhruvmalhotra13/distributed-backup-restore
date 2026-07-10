using BackupRestore.Core.Enums;

namespace BackupRestore.Core.Contracts;

/// <summary>
/// Live progress snapshot published to Redis and relayed to the UI via SignalR.
/// </summary>
public record ProgressUpdate
{
    public Guid JobId { get; init; }

    /// <summary>"Backup" or "Restore".</summary>
    public string JobType { get; init; } = string.Empty;

    public JobStatus Status { get; init; }

    public long TotalBytes { get; init; }

    public long ProcessedBytes { get; init; }

    public double ProgressPercent { get; init; }

    public int TotalFiles { get; init; }

    public int FilesProcessed { get; init; }

    public double ThroughputBytesPerSec { get; init; }

    public string? Message { get; init; }

    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
