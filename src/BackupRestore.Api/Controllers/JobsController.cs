using BackupRestore.Api.Dtos;
using BackupRestore.Core.Abstractions;
using BackupRestore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackupRestore.Api.Controllers;

[ApiController]
[Route("jobs")]
public class JobsController : ControllerBase
{
    private readonly BackupDbContext _db;
    private readonly IProgressPublisher _progress;

    public JobsController(BackupDbContext db, IProgressPublisher progress)
    {
        _db = db;
        _progress = progress;
    }

    /// <summary>FR15: View a job's event timeline (backup or restore).</summary>
    [HttpGet("{id:guid}/events")]
    public async Task<ActionResult<IEnumerable<JobEventResponse>>> Events(Guid id, CancellationToken ct)
    {
        var events = await _db.JobEvents.AsNoTracking()
            .Where(x => x.JobId == id)
            .OrderBy(x => x.Timestamp)
            .ToListAsync(ct);

        return Ok(events.Select(JobEventResponse.From));
    }

    /// <summary>Polling fallback for the latest cached progress snapshot.</summary>
    [HttpGet("{id:guid}/progress")]
    public async Task<IActionResult> Progress(Guid id, CancellationToken ct)
    {
        var snapshot = await _progress.GetLatestAsync(id, ct);
        return snapshot is null ? NoContent() : Ok(snapshot);
    }
}
