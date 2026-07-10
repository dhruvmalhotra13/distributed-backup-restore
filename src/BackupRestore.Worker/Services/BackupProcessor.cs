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
/// The Backup DataMover. Scans the source folder, chunks files, streams bytes
/// into the Backup Vault, checkpoints after each chunk, publishes progress, and
/// validates integrity via SHA-256 hashes. Supports pause/resume/cancel and
/// crash recovery from the last checkpoint.
/// </summary>
public class BackupProcessor
{
    private readonly BackupDbContext _db;
    private readonly IBackupVault _vault;
    private readonly IProgressPublisher _progress;
    private readonly IJobControlStore _control;
    private readonly ILogger<BackupProcessor> _logger;

    private DateTime _lastPublish = DateTime.MinValue;
    private static readonly TimeSpan PublishInterval = TimeSpan.FromMilliseconds(500);

    public BackupProcessor(
        BackupDbContext db,
        IBackupVault vault,
        IProgressPublisher progress,
        IJobControlStore control,
        ILogger<BackupProcessor> logger)
    {
        _db = db;
        _vault = vault;
        _progress = progress;
        _control = control;
        _logger = logger;
    }

    public async Task ProcessAsync(BackupRequested message, CancellationToken cancellationToken)
    {
        var job = await _db.BackupJobs.FirstOrDefaultAsync(x => x.Id == message.JobId, cancellationToken);
        if (job is null)
        {
            _logger.LogWarning("Backup job {JobId} not found; ignoring.", message.JobId);
            return;
        }

        if (job.Status is JobStatus.Completed or JobStatus.Cancelled)
        {
            _logger.LogInformation("Backup job {JobId} already {Status}; ignoring.", job.Id, job.Status);
            return;
        }

        var chunkSize = message.ChunkSizeBytes > 0 ? message.ChunkSizeBytes : 4 * 1024 * 1024;

        try
        {
            job.Status = JobStatus.Running;
            job.UpdatedAt = DateTime.UtcNow;
            await AddEventAsync(job.Id, "Running", "Backup started.", cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await _vault.AppendLogAsync(job.BackupId, "Backup started.", cancellationToken);

            await ScanIfNeededAsync(job, chunkSize, cancellationToken);

            var completed = await TransferAsync(job, chunkSize, cancellationToken);
            if (!completed)
            {
                return; // paused or cancelled; state already persisted
            }

            await FinalizeAsync(job, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException ex) when (IsDiskFull(ex))
        {
            await FailAsync(job, "StorageFull", "Backup failed: disk is full.", cancellationToken);
        }
        catch (FileNotFoundException ex)
        {
            await FailAsync(job, "SourceMissing", $"Backup failed: source file missing ({ex.FileName}).", cancellationToken);
        }
        catch (DirectoryNotFoundException)
        {
            await FailAsync(job, "SourceMissing", "Backup failed: source directory missing.", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup job {JobId} failed.", job.Id);
            await FailAsync(job, "Error", $"Backup failed: {ex.Message}", cancellationToken);
        }
    }

    private async Task ScanIfNeededAsync(BackupJob job, int chunkSize, CancellationToken ct)
    {
        var alreadyScanned = await _db.BackupFiles.AnyAsync(x => x.BackupJobId == job.Id, ct);
        if (alreadyScanned)
        {
            return;
        }

        long totalBytes = 0;
        var files = new List<BackupFile>();

        var entries = Directory.EnumerateFiles(job.SourcePath, "*", SearchOption.AllDirectories);
        foreach (var absolutePath in entries)
        {
            ct.ThrowIfCancellationRequested();
            var info = new FileInfo(absolutePath);
            var relative = Path.GetRelativePath(job.SourcePath, absolutePath).Replace('\\', '/');
            var chunkCount = info.Length == 0 ? 0 : (int)Math.Ceiling((double)info.Length / chunkSize);

            files.Add(new BackupFile
            {
                BackupJobId = job.Id,
                BackupId = job.BackupId,
                RelativePath = relative,
                SizeBytes = info.Length,
                LastModifiedUtc = info.LastWriteTimeUtc,
                ChunkCount = chunkCount
            });
            totalBytes += info.Length;
        }

        _db.BackupFiles.AddRange(files);
        job.TotalBytes = totalBytes;
        job.TotalFiles = files.Count;
        job.UpdatedAt = DateTime.UtcNow;
        await AddEventAsync(job.Id, "Scanned", $"Scanned {files.Count} files, {totalBytes} bytes.", ct);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Returns true if the transfer completed; false if paused/cancelled.</summary>
    private async Task<bool> TransferAsync(BackupJob job, int chunkSize, CancellationToken ct)
    {
        var files = await _db.BackupFiles
            .Where(x => x.BackupJobId == job.Id)
            .OrderBy(x => x.RelativePath)
            .ToListAsync(ct);

        // Baseline copied bytes from existing checkpoints (resume support).
        var checkpoints = await _db.Checkpoints
            .Where(x => x.BackupJobId == job.Id)
            .ToDictionaryAsync(x => x.BackupFileId, ct);

        job.CopiedBytes = ProgressCalculator.Monotonic(job.CopiedBytes, checkpoints.Values.Sum(c => c.BytesCompleted));
        job.FilesProcessed = files.Count(f => f.FileHash is not null);

        // Incremental: files from the previous version of this backup set, keyed by path.
        var previousFiles = await LoadPreviousFilesAsync(job, ct);

        var stopwatch = Stopwatch.StartNew();
        var bytesAtStart = job.CopiedBytes;

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();

            if (file.FileHash is not null)
            {
                continue; // already fully backed up
            }

            // Incremental fast-path: unchanged since the previous version
            // (same path, size and modified time) -> reuse its chunks, copy no bytes.
            if (previousFiles.TryGetValue(file.RelativePath, out var prev) &&
                prev.SizeBytes == file.SizeBytes &&
                prev.LastModifiedUtc == file.LastModifiedUtc &&
                prev.FileHash is not null)
            {
                foreach (var pc in prev.Chunks.OrderBy(c => c.ChunkIndex))
                {
                    _db.BackupChunks.Add(new BackupChunk
                    {
                        BackupFileId = file.Id,
                        ChunkIndex = pc.ChunkIndex,
                        ChunkSize = pc.ChunkSize,
                        ChunkHash = pc.ChunkHash,
                        VaultPath = pc.VaultPath,
                        Status = ChunkStatus.Completed
                    });
                }

                file.FileHash = prev.FileHash;
                job.CopiedBytes = ProgressCalculator.Monotonic(job.CopiedBytes, job.CopiedBytes + file.SizeBytes);
                job.ProgressPercent = ProgressCalculator.Percent(job.CopiedBytes, job.TotalBytes);
                job.DedupedBytes += file.SizeBytes;
                job.FilesProcessed++;
                job.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
                await MaybePublishAsync(job, stopwatch, bytesAtStart, force: false, ct);
                continue;
            }

            checkpoints.TryGetValue(file.Id, out var checkpoint);
            var startIndex = (checkpoint?.LastCompletedChunkIndex ?? -1) + 1;

            var control = await ProcessFileChunksAsync(job, file, checkpoint, startIndex, chunkSize, stopwatch, bytesAtStart, ct);
            if (control == JobControlSignal.Pause)
            {
                await SetPausedAsync(job, ct);
                return false;
            }
            if (control == JobControlSignal.Cancel)
            {
                await SetCancelledAsync(job, ct);
                return false;
            }

            // File finished: compute whole-file hash and mark processed.
            var sourceFullPath = Path.Combine(job.SourcePath, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            file.FileHash = await HashUtil.ComputeFileHexAsync(sourceFullPath, ct);
            job.FilesProcessed++;
            job.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return true;
    }

    /// <summary>
    /// Loads the files (with chunks) of the most recent completed backup that
    /// shares this job's BackupName, so unchanged files can be reused (incremental).
    /// </summary>
    private async Task<Dictionary<string, BackupFile>> LoadPreviousFilesAsync(BackupJob job, CancellationToken ct)
    {
        var previous = await _db.BackupJobs.AsNoTracking()
            .Where(x => x.BackupName == job.BackupName && x.Id != job.Id && x.Status == JobStatus.Completed)
            .OrderByDescending(x => x.Version)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (previous is null)
        {
            return new Dictionary<string, BackupFile>();
        }

        var files = await _db.BackupFiles.AsNoTracking()
            .Where(x => x.BackupJobId == previous.Id)
            .Include(x => x.Chunks)
            .ToListAsync(ct);

        return files.ToDictionary(f => f.RelativePath, f => f);
    }

    private async Task<JobControlSignal> ProcessFileChunksAsync(
        BackupJob job, BackupFile file, Checkpoint? checkpoint, int startIndex,
        int chunkSize, Stopwatch stopwatch, long bytesAtStart, CancellationToken ct)
    {
        if (file.ChunkCount == 0)
        {
            return JobControlSignal.None; // empty file, nothing to copy
        }

        var sourceFullPath = Path.Combine(job.SourcePath, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));

        await using var source = new FileStream(
            sourceFullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 1 << 16, useAsync: true);
        source.Seek((long)startIndex * chunkSize, SeekOrigin.Begin);

        var buffer = new byte[chunkSize];

        for (var index = startIndex; index < file.ChunkCount; index++)
        {
            // Cooperative control check between chunks.
            var signal = await _control.GetSignalAsync(job.Id, ct);
            if (signal is JobControlSignal.Pause or JobControlSignal.Cancel)
            {
                return signal;
            }

            var bytesRead = await ReadChunkAsync(source, buffer, ct);
            if (bytesRead == 0)
            {
                break;
            }

            var data = new ReadOnlyMemory<byte>(buffer, 0, bytesRead);
            var chunkHash = HashUtil.ComputeHex(data.Span);

            // Content-addressed dedup: only write the chunk if these exact bytes
            // are not already in the vault (shared across all files and backups).
            var vaultPath = _vault.GetCasChunkPath(chunkHash);
            if (_vault.ChunkExists(vaultPath))
            {
                job.DedupedBytes += bytesRead;
            }
            else
            {
                await _vault.WriteChunkAsync(vaultPath, data, ct);
            }

            await UpsertChunkAsync(file.Id, index, bytesRead, chunkHash, vaultPath, ct);

            checkpoint = await UpsertCheckpointAsync(job.Id, file.Id, index, chunkSize, bytesRead, checkpoint, ct);

            job.CopiedBytes = ProgressCalculator.Monotonic(job.CopiedBytes, job.CopiedBytes + bytesRead);
            job.ProgressPercent = ProgressCalculator.Percent(job.CopiedBytes, job.TotalBytes);

            await _db.SaveChangesAsync(ct);
            await MaybePublishAsync(job, stopwatch, bytesAtStart, force: false, ct);
        }

        return JobControlSignal.None;
    }

    private static async Task<int> ReadChunkAsync(Stream source, byte[] buffer, CancellationToken ct)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await source.ReadAsync(buffer.AsMemory(total, buffer.Length - total), ct);
            if (read == 0) break;
            total += read;
        }
        return total;
    }

    private async Task UpsertChunkAsync(Guid fileId, int index, long size, string hash, string vaultPath, CancellationToken ct)
    {
        var chunk = await _db.BackupChunks.FirstOrDefaultAsync(x => x.BackupFileId == fileId && x.ChunkIndex == index, ct);
        if (chunk is null)
        {
            chunk = new BackupChunk { BackupFileId = fileId, ChunkIndex = index };
            _db.BackupChunks.Add(chunk);
        }

        chunk.ChunkSize = size;
        chunk.ChunkHash = hash;
        chunk.VaultPath = vaultPath;
        chunk.Status = ChunkStatus.Completed;
    }

    private async Task<Checkpoint> UpsertCheckpointAsync(
        Guid jobId, Guid fileId, int index, int chunkSize, long bytesRead, Checkpoint? existing, CancellationToken ct)
    {
        existing ??= await _db.Checkpoints.FirstOrDefaultAsync(x => x.BackupJobId == jobId && x.BackupFileId == fileId, ct);
        if (existing is null)
        {
            existing = new Checkpoint { BackupJobId = jobId, BackupFileId = fileId };
            _db.Checkpoints.Add(existing);
        }

        existing.LastCompletedChunkIndex = index;
        existing.BytesCompleted = (long)index * chunkSize + bytesRead;
        existing.UpdatedAt = DateTime.UtcNow;
        return existing;
    }

    private async Task FinalizeAsync(BackupJob job, CancellationToken ct)
    {
        var files = await _db.BackupFiles
            .Where(x => x.BackupJobId == job.Id)
            .OrderBy(x => x.RelativePath)
            .ToListAsync(ct);

        var manifest = new
        {
            backupId = job.BackupId,
            backupName = job.BackupName,
            version = job.Version,
            sourcePath = job.SourcePath,
            totalBytes = job.TotalBytes,
            storedBytes = job.TotalBytes - job.DedupedBytes,
            dedupedBytes = job.DedupedBytes,
            totalFiles = job.TotalFiles,
            createdAt = job.CreatedAt,
            files = files.Select(f => new
            {
                f.Id,
                f.RelativePath,
                f.SizeBytes,
                f.ChunkCount,
                f.FileHash
            })
        };

        var json = System.Text.Json.JsonSerializer.Serialize(manifest,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await _vault.WriteTextAsync(job.BackupId, "manifest.json", json, ct);

        var metadata = System.Text.Json.JsonSerializer.Serialize(new
        {
            job.BackupId,
            job.BackupName,
            job.Version,
            job.TotalBytes,
            StoredBytes = job.TotalBytes - job.DedupedBytes,
            job.DedupedBytes,
            job.TotalFiles,
            completedAt = DateTime.UtcNow
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await _vault.WriteTextAsync(job.BackupId, "metadata.json", metadata, ct);

        var hashes = System.Text.Json.JsonSerializer.Serialize(
            files.ToDictionary(f => f.RelativePath, f => f.FileHash),
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await _vault.WriteTextAsync(job.BackupId, "hashes/file_hashes.json", hashes, ct);

        job.Status = JobStatus.Completed;
        job.CopiedBytes = job.TotalBytes;
        job.ProgressPercent = 100d;
        job.UpdatedAt = DateTime.UtcNow;
        var stored = job.TotalBytes - job.DedupedBytes;
        var savedPct = job.TotalBytes == 0 ? 0 : (int)Math.Round(100.0 * job.DedupedBytes / job.TotalBytes);
        await AddEventAsync(job.Id, "Completed",
            $"Backup v{job.Version} completed. Stored {stored} of {job.TotalBytes} bytes ({savedPct}% deduplicated).", ct);
        await _db.SaveChangesAsync(ct);
        await _vault.AppendLogAsync(job.BackupId, "Backup completed.", ct);
        await PublishAsync(job, "Backup completed.", ct);
    }

    private async Task SetPausedAsync(BackupJob job, CancellationToken ct)
    {
        job.Status = JobStatus.Paused;
        job.UpdatedAt = DateTime.UtcNow;
        await AddEventAsync(job.Id, "Paused", "Backup paused at checkpoint.", ct);
        await _db.SaveChangesAsync(ct);
        await PublishAsync(job, "Backup paused.", ct);
    }

    private async Task SetCancelledAsync(BackupJob job, CancellationToken ct)
    {
        job.Status = JobStatus.Cancelled;
        job.UpdatedAt = DateTime.UtcNow;
        await AddEventAsync(job.Id, "Cancelled", "Backup cancelled by user.", ct);
        await _db.SaveChangesAsync(ct);
        await PublishAsync(job, "Backup cancelled.", ct);
    }

    private async Task FailAsync(BackupJob job, string reason, string message, CancellationToken ct)
    {
        job.Status = JobStatus.Failed;
        job.ErrorMessage = message;
        job.UpdatedAt = DateTime.UtcNow;
        await AddEventAsync(job.Id, reason, message, ct);
        await _db.SaveChangesAsync(ct);
        await _vault.AppendLogAsync(job.BackupId, message, ct);
        await PublishAsync(job, message, ct);
    }

    private async Task MaybePublishAsync(BackupJob job, Stopwatch stopwatch, long bytesAtStart, bool force, CancellationToken ct)
    {
        if (!force && DateTime.UtcNow - _lastPublish < PublishInterval)
        {
            return;
        }
        _lastPublish = DateTime.UtcNow;

        var seconds = Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
        var throughput = (job.CopiedBytes - bytesAtStart) / seconds;
        await _progress.PublishAsync(BuildUpdate(job, throughput, null), ct);
    }

    private Task PublishAsync(BackupJob job, string? message, CancellationToken ct)
        => _progress.PublishAsync(BuildUpdate(job, 0, message), ct);

    private static ProgressUpdate BuildUpdate(BackupJob job, double throughput, string? message) => new()
    {
        JobId = job.Id,
        JobType = "Backup",
        Status = job.Status,
        TotalBytes = job.TotalBytes,
        ProcessedBytes = job.CopiedBytes,
        ProgressPercent = job.ProgressPercent,
        TotalFiles = job.TotalFiles,
        FilesProcessed = job.FilesProcessed,
        ThroughputBytesPerSec = throughput,
        Message = message
    };

    private Task AddEventAsync(Guid jobId, string type, string message, CancellationToken ct)
    {
        _db.JobEvents.Add(new JobEvent
        {
            JobId = jobId,
            JobType = "Backup",
            EventType = type,
            Message = message
        });
        return Task.CompletedTask;
    }

    private static bool IsDiskFull(IOException ex)
    {
        // ERROR_DISK_FULL (0x70) / ERROR_HANDLE_DISK_FULL (0x27) on Windows; ENOSPC(28) on Linux.
        var hr = ex.HResult & 0xFFFF;
        return hr is 0x70 or 0x27 or 28;
    }
}
