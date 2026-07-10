using BackupRestore.Core.Contracts;

namespace BackupRestore.Core.Abstractions;

/// <summary>
/// Publishes live progress snapshots to a pub/sub channel and caches the latest
/// snapshot for polling fallback.
/// </summary>
public interface IProgressPublisher
{
    Task PublishAsync(ProgressUpdate update, CancellationToken cancellationToken);

    /// <summary>Returns the last cached snapshot for a job, if any.</summary>
    Task<ProgressUpdate?> GetLatestAsync(Guid jobId, CancellationToken cancellationToken);
}
