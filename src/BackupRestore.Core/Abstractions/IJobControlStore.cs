using BackupRestore.Core.Contracts;

namespace BackupRestore.Core.Abstractions;

/// <summary>
/// Stores cooperative control signals (pause/resume/cancel) that a running job
/// polls between chunks.
/// </summary>
public interface IJobControlStore
{
    Task SetSignalAsync(Guid jobId, JobControlSignal signal, CancellationToken cancellationToken);

    Task<JobControlSignal> GetSignalAsync(Guid jobId, CancellationToken cancellationToken);

    Task ClearAsync(Guid jobId, CancellationToken cancellationToken);
}
