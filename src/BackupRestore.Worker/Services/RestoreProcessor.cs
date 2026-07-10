using System.Diagnostics;
using BackupRestore.Core;
using BackupRestore.Core.Abstractions;
using BackupRestore.Core.Contracts;
using BackupRestore.Core.Entities;
using BackupRestore.Core.Enums;
using BackupRestore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BackupRestore.Worker.Services;

/// <summary>
/// The Restore DataMover. Reads backup metadata and chunks from the vault,
/// reconstructs files into the restore target, tracks progress, and validates
/// integrity by comparing restored file hashes with the stored originals.
/// </summary>
public class RestoreProcessor
{
    private readonly BackupDbContext _db;
    private readonly IBackupVault _vault;
    private readonly IProgressPublisher _progress;
    private readonly ILogger<RestoreProcessor> _logger;

    private DateTime _lastPublish = DateTime.MinValue;
    private static readonly TimeSpan PublishInterval = TimeSpan.FromMilliseconds(500);

    public RestoreProcessor(
        BackupDbContext db,
        IBackupVault vault,
        IProgressPublisher progress,
        ILogger<RestoreProcessor> logger)
    {
        _db = db;
        _vault = vault;
        _progress = progress;
        _logger = logger;
    }

    public async Task ProcessAsync(RestoreRequested message, CancellationToken cancellationToken)
    {
        var job = await _db.RestoreJobs.FirstOrDefaultAsync(x => x.Id == message.RestoreJobId, cancellationToken);
        if (job is null)
        {
            _logger.LogWarning("Restore job {JobId} not found; ignoring.", message.RestoreJobId);
            return;
        }

        if (job.Status is JobStatus.Completed or JobStatus.Cancelled)
        {
            return;
        }

        try
        {
            job.Status = JobStatus.Running;
            job.RestoredBytes = 0;
            job.UpdatedAt = DateTime.UtcNow;
            await AddEventAsync(job.Id, "Running", "Restore started.", cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);

            var backup = await _db.BackupJobs.AsNoTracking()
                .FirstOrDefaultAsync(x => x.BackupId == message.BackupId, cancellationToken);
            if (backup is null)
            {
                await FailAsync(job, "BackupMissing", $"Backup '{message.BackupId}' not found.", cancellationToken);
                return;
            }

            job.TotalBytes = backup.TotalBytes;

            var files = await _db.BackupFiles
                .Where(x => x.BackupJobId == backup.Id)
                .OrderBy(x => x.RelativePath)
                .ToListAsync(cancellationToken);

            Directory.CreateDirectory(job.RestorePath);

            var stopwatch = Stopwatch.StartNew();

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var completed = await RestoreFileAsync(job, file, stopwatch, cancellationToken);
                if (!completed)
                {
                    return; // integrity failure already recorded
                }
            }

            job.Status = JobStatus.Completed;
            job.RestoredBytes = job.TotalBytes;
            job.ProgressPercent = 100d;
            job.UpdatedAt = DateTime.UtcNow;
            await AddEventAsync(job.Id, "Completed", "Restore completed; all files validated.", cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await PublishAsync(job, "Restore completed.", cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore job {JobId} failed.", job.Id);
            await FailAsync(job, "Error", $"Restore failed: {ex.Message}", cancellationToken);
        }
    }

    /// <summary>Returns true on success, false if integrity validation failed.</summary>
    private async Task<bool> RestoreFileAsync(RestoreJob job, BackupFile file, Stopwatch stopwatch, CancellationToken ct)
    {
        var targetPath = Path.Combine(job.RestorePath, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        var chunks = await _db.BackupChunks.AsNoTracking()
            .Where(x => x.BackupFileId == file.Id)
            .OrderBy(x => x.ChunkIndex)
            .ToListAsync(ct);

        await using (var target = new FileStream(
            targetPath, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 1 << 16, useAsync: true))
        {
            foreach (var chunk in chunks)
            {
                ct.ThrowIfCancellationRequested();

                await using var chunkStream = _vault.OpenChunkRead(chunk.VaultPath);
                await chunkStream.CopyToAsync(target, ct);

                job.RestoredBytes = ProgressCalculator.Monotonic(job.RestoredBytes, job.RestoredBytes + chunk.ChunkSize);
                job.ProgressPercent = ProgressCalculator.Percent(job.RestoredBytes, job.TotalBytes);
                await MaybePublishAsync(job, stopwatch, ct);
            }
        }

        // FR13: validate integrity of the restored file.
        if (file.FileHash is not null)
        {
            var restoredHash = await HashUtil.ComputeFileHexAsync(targetPath, ct);
            if (!string.Equals(restoredHash, file.FileHash, StringComparison.OrdinalIgnoreCase))
            {
                await FailAsync(job, "IntegrityMismatch",
                    $"Integrity check failed for '{file.RelativePath}'.", ct);
                return false;
            }
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task FailAsync(RestoreJob job, string reason, string message, CancellationToken ct)
    {
        job.Status = JobStatus.Failed;
        job.ErrorMessage = message;
        job.UpdatedAt = DateTime.UtcNow;
        await AddEventAsync(job.Id, reason, message, ct);
        await _db.SaveChangesAsync(ct);
        await PublishAsync(job, message, ct);
    }

    private async Task MaybePublishAsync(RestoreJob job, Stopwatch stopwatch, CancellationToken ct)
    {
        if (DateTime.UtcNow - _lastPublish < PublishInterval)
        {
            return;
        }
        _lastPublish = DateTime.UtcNow;

        var seconds = Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
        var throughput = job.RestoredBytes / seconds;
        await _progress.PublishAsync(BuildUpdate(job, throughput, null), ct);
    }

    private Task PublishAsync(RestoreJob job, string? message, CancellationToken ct)
        => _progress.PublishAsync(BuildUpdate(job, 0, message), ct);

    private static ProgressUpdate BuildUpdate(RestoreJob job, double throughput, string? message) => new()
    {
        JobId = job.Id,
        JobType = "Restore",
        Status = job.Status,
        TotalBytes = job.TotalBytes,
        ProcessedBytes = job.RestoredBytes,
        ProgressPercent = job.ProgressPercent,
        ThroughputBytesPerSec = throughput,
        Message = message
    };

    private Task AddEventAsync(Guid jobId, string type, string message, CancellationToken ct)
    {
        _db.JobEvents.Add(new JobEvent
        {
            JobId = jobId,
            JobType = "Restore",
            EventType = type,
            Message = message
        });
        return Task.CompletedTask;
    }
}
