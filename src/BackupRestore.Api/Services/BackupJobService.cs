using BackupRestore.Core;
using BackupRestore.Core.Contracts;
using BackupRestore.Core.Entities;
using BackupRestore.Core.Enums;
using BackupRestore.Infrastructure.Options;
using BackupRestore.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BackupRestore.Api.Services;

/// <summary>
/// Creates and queues backup jobs. Shared by the HTTP controller and the
/// scheduler so versioning and publishing behave identically for both.
/// </summary>
public class BackupJobService
{
    private readonly BackupDbContext _db;
    private readonly IPublishEndpoint _publish;
    private readonly StorageOptions _storage;

    public BackupJobService(BackupDbContext db, IPublishEndpoint publish, IOptions<StorageOptions> storage)
    {
        _db = db;
        _publish = publish;
        _storage = storage.Value;
    }

    /// <summary>
    /// Persists a new backup job (assigning the next version within its backup
    /// set) and publishes the command for a worker to process.
    /// </summary>
    public async Task<BackupJob> CreateAndQueueAsync(
        string containerSourcePath, string backupName, int? chunkSizeBytes, string createdVia, CancellationToken ct)
    {
        var lastVersion = await _db.BackupJobs
            .Where(x => x.BackupName == backupName)
            .Select(x => (int?)x.Version)
            .MaxAsync(ct) ?? 0;

        var job = new BackupJob
        {
            BackupId = BackupIdFactory.NewId(),
            BackupName = backupName,
            SourcePath = containerSourcePath,
            Version = lastVersion + 1,
            Status = JobStatus.Queued
        };

        _db.BackupJobs.Add(job);
        _db.JobEvents.Add(new JobEvent
        {
            JobId = job.Id,
            JobType = "Backup",
            EventType = "Created",
            Message = $"Backup v{job.Version} queued for '{containerSourcePath}' ({createdVia})."
        });
        await _db.SaveChangesAsync(ct);

        await _publish.Publish(new BackupRequested
        {
            JobId = job.Id,
            BackupId = job.BackupId,
            BackupName = job.BackupName,
            SourcePath = job.SourcePath,
            ChunkSizeBytes = chunkSizeBytes ?? _storage.ChunkSizeBytes
        }, ct);

        return job;
    }
}
