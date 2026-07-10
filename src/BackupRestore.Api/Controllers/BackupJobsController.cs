using BackupRestore.Api.Dtos;
using BackupRestore.Core;
using BackupRestore.Core.Abstractions;
using BackupRestore.Core.Contracts;
using BackupRestore.Core.Entities;
using BackupRestore.Core.Enums;
using BackupRestore.Infrastructure.Options;
using BackupRestore.Infrastructure.Persistence;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BackupRestore.Api.Controllers;

[ApiController]
[Route("backup-jobs")]
public class BackupJobsController : ControllerBase
{
    private readonly BackupDbContext _db;
    private readonly IPublishEndpoint _publish;
    private readonly IJobControlStore _control;
    private readonly StorageOptions _storage;

    public BackupJobsController(
        BackupDbContext db,
        IPublishEndpoint publish,
        IJobControlStore control,
        IOptions<StorageOptions> storage)
    {
        _db = db;
        _publish = publish;
        _control = control;
        _storage = storage.Value;
    }

    /// <summary>FR1: Create a backup job for a source folder.</summary>
    [HttpPost]
    public async Task<ActionResult<BackupJobResponse>> Create(
        [FromBody] CreateBackupJobRequest request, CancellationToken ct)
    {
        if (!Directory.Exists(request.SourcePath))
        {
            return BadRequest(new { error = $"Source path not found: {request.SourcePath}" });
        }

        var chunkSize = request.ChunkSizeBytes ?? _storage.ChunkSizeBytes;

        var job = new BackupJob
        {
            BackupId = BackupIdFactory.NewId(),
            BackupName = request.BackupName,
            SourcePath = request.SourcePath,
            Status = JobStatus.Queued
        };

        _db.BackupJobs.Add(job);
        _db.JobEvents.Add(NewEvent(job.Id, "Created", $"Backup job queued for '{request.SourcePath}'."));
        await _db.SaveChangesAsync(ct);

        await _publish.Publish(new BackupRequested
        {
            JobId = job.Id,
            BackupId = job.BackupId,
            BackupName = job.BackupName,
            SourcePath = job.SourcePath,
            ChunkSizeBytes = chunkSize
        }, ct);

        return CreatedAtAction(nameof(GetById), new { id = job.Id }, BackupJobResponse.From(job));
    }

    /// <summary>FR14: List backup jobs, optionally filtered by status.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<BackupJobResponse>>> List(
        [FromQuery] string? status, CancellationToken ct)
    {
        var query = _db.BackupJobs.AsNoTracking().OrderByDescending(x => x.CreatedAt).AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<JobStatus>(status, true, out var parsed))
        {
            query = query.Where(x => x.Status == parsed);
        }

        var jobs = await query.Take(200).ToListAsync(ct);
        return Ok(jobs.Select(BackupJobResponse.From));
    }

    /// <summary>FR15: Get a single backup job's details.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BackupJobResponse>> GetById(Guid id, CancellationToken ct)
    {
        var job = await _db.BackupJobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return job is null ? NotFound() : Ok(BackupJobResponse.From(job));
    }

    /// <summary>FR6: Pause a running backup.</summary>
    [HttpPost("{id:guid}/pause")]
    public async Task<IActionResult> Pause(Guid id, CancellationToken ct)
    {
        var job = await _db.BackupJobs.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (job is null) return NotFound();
        if (job.Status != JobStatus.Running)
        {
            return Conflict(new { error = $"Cannot pause a job in status {job.Status}." });
        }

        await _control.SetSignalAsync(id, JobControlSignal.Pause, ct);
        _db.JobEvents.Add(NewEvent(id, "PauseRequested", "Pause requested by user."));
        await _db.SaveChangesAsync(ct);
        return Accepted();
    }

    /// <summary>FR7: Resume a paused or failed backup from its checkpoint.</summary>
    [HttpPost("{id:guid}/resume")]
    public async Task<IActionResult> Resume(Guid id, CancellationToken ct)
    {
        var job = await _db.BackupJobs.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (job is null) return NotFound();
        if (job.Status is not (JobStatus.Paused or JobStatus.Failed))
        {
            return Conflict(new { error = $"Cannot resume a job in status {job.Status}." });
        }

        await _control.ClearAsync(id, ct);
        job.Status = JobStatus.Queued;
        job.UpdatedAt = DateTime.UtcNow;
        _db.JobEvents.Add(NewEvent(id, "ResumeRequested", "Resume requested; re-queued from checkpoint."));
        await _db.SaveChangesAsync(ct);

        await RepublishAsync(job, ct);
        return Accepted();
    }

    /// <summary>FR8: Cancel a backup job.</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var job = await _db.BackupJobs.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (job is null) return NotFound();
        if (job.Status is JobStatus.Completed or JobStatus.Cancelled)
        {
            return Conflict(new { error = $"Cannot cancel a job in status {job.Status}." });
        }

        await _control.SetSignalAsync(id, JobControlSignal.Cancel, ct);

        // If the worker hasn't started yet, cancel immediately.
        if (job.Status == JobStatus.Queued)
        {
            job.Status = JobStatus.Cancelled;
            job.UpdatedAt = DateTime.UtcNow;
        }

        _db.JobEvents.Add(NewEvent(id, "CancelRequested", "Cancel requested by user."));
        await _db.SaveChangesAsync(ct);
        return Accepted();
    }

    /// <summary>FR9: Retry a failed or cancelled backup job.</summary>
    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct)
    {
        var job = await _db.BackupJobs.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (job is null) return NotFound();
        if (job.Status is not (JobStatus.Failed or JobStatus.PartiallyFailed or JobStatus.Cancelled))
        {
            return Conflict(new { error = $"Cannot retry a job in status {job.Status}." });
        }

        await _control.ClearAsync(id, ct);
        job.Status = JobStatus.Queued;
        job.ErrorMessage = null;
        job.UpdatedAt = DateTime.UtcNow;
        _db.JobEvents.Add(NewEvent(id, "RetryRequested", "Retry requested; re-queued from checkpoint."));
        await _db.SaveChangesAsync(ct);

        await RepublishAsync(job, ct);
        return Accepted();
    }

    private Task RepublishAsync(BackupJob job, CancellationToken ct)
        => _publish.Publish(new BackupRequested
        {
            JobId = job.Id,
            BackupId = job.BackupId,
            BackupName = job.BackupName,
            SourcePath = job.SourcePath,
            ChunkSizeBytes = _storage.ChunkSizeBytes
        }, ct);

    private static JobEvent NewEvent(Guid jobId, string type, string message) => new()
    {
        JobId = jobId,
        JobType = "Backup",
        EventType = type,
        Message = message
    };
}
