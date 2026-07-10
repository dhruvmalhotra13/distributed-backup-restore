using BackupRestore.Api.Dtos;
using BackupRestore.Core.Contracts;
using BackupRestore.Core.Entities;
using BackupRestore.Core.Enums;
using BackupRestore.Infrastructure.Persistence;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackupRestore.Api.Controllers;

[ApiController]
[Route("restore-jobs")]
public class RestoreJobsController : ControllerBase
{
    private readonly BackupDbContext _db;
    private readonly IPublishEndpoint _publish;

    public RestoreJobsController(BackupDbContext db, IPublishEndpoint publish)
    {
        _db = db;
        _publish = publish;
    }

    /// <summary>FR10: Create a restore job for a completed backup version.</summary>
    [HttpPost]
    public async Task<ActionResult<RestoreJobResponse>> Create(
        [FromBody] CreateRestoreJobRequest request, CancellationToken ct)
    {
        var backup = await _db.BackupJobs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.BackupId == request.BackupId, ct);

        if (backup is null)
        {
            return BadRequest(new { error = $"Unknown backupId: {request.BackupId}" });
        }

        if (backup.Status != JobStatus.Completed)
        {
            return BadRequest(new { error = $"Backup '{request.BackupId}' is not completed (status {backup.Status})." });
        }

        var job = new RestoreJob
        {
            BackupId = request.BackupId,
            RestorePath = request.RestorePath,
            Status = JobStatus.Queued,
            TotalBytes = backup.TotalBytes
        };

        _db.RestoreJobs.Add(job);
        _db.JobEvents.Add(new JobEvent
        {
            JobId = job.Id,
            JobType = "Restore",
            EventType = "Created",
            Message = $"Restore job queued for backup '{request.BackupId}' into '{request.RestorePath}'."
        });
        await _db.SaveChangesAsync(ct);

        await _publish.Publish(new RestoreRequested
        {
            RestoreJobId = job.Id,
            BackupId = job.BackupId,
            RestorePath = job.RestorePath
        }, ct);

        return CreatedAtAction(nameof(GetById), new { id = job.Id }, RestoreJobResponse.From(job));
    }

    /// <summary>List restore jobs.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RestoreJobResponse>>> List(CancellationToken ct)
    {
        var jobs = await _db.RestoreJobs.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(ct);
        return Ok(jobs.Select(RestoreJobResponse.From));
    }

    /// <summary>FR12: Get restore job details/progress.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RestoreJobResponse>> GetById(Guid id, CancellationToken ct)
    {
        var job = await _db.RestoreJobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return job is null ? NotFound() : Ok(RestoreJobResponse.From(job));
    }
}
