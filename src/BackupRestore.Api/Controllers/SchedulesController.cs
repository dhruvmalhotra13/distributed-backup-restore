using BackupRestore.Api.Dtos;
using BackupRestore.Api.Services;
using BackupRestore.Core.Entities;
using BackupRestore.Infrastructure.Persistence;
using Cronos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackupRestore.Api.Controllers;

[ApiController]
[Route("schedules")]
public class SchedulesController : ControllerBase
{
    private readonly BackupDbContext _db;
    private readonly HostPathTranslator _paths;

    public SchedulesController(BackupDbContext db, HostPathTranslator paths)
    {
        _db = db;
        _paths = paths;
    }

    /// <summary>Create a recurring backup schedule.</summary>
    [HttpPost]
    public async Task<ActionResult<ScheduleResponse>> Create(
        [FromBody] CreateScheduleRequest request, CancellationToken ct)
    {
        if (!CronExpression.TryParse(request.CronExpression, CronFormat.Standard, out var cron))
        {
            return BadRequest(new { error = $"Invalid cron expression: '{request.CronExpression}'. Use 5 fields, e.g. '0 2 * * *'." });
        }

        var sourcePath = _paths.ToContainerPath(request.SourcePath);
        if (!Directory.Exists(sourcePath))
        {
            return BadRequest(new { error = $"Source path not found: {request.SourcePath}" });
        }

        var schedule = new BackupSchedule
        {
            Name = request.Name,
            SourcePath = sourcePath,
            CronExpression = request.CronExpression,
            Enabled = true,
            NextRunAt = cron.GetNextOccurrence(DateTime.UtcNow)
        };

        _db.BackupSchedules.Add(schedule);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = schedule.Id }, ScheduleResponse.From(schedule));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ScheduleResponse>>> List(CancellationToken ct)
    {
        var schedules = await _db.BackupSchedules.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
        return Ok(schedules.Select(ScheduleResponse.From));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ScheduleResponse>> GetById(Guid id, CancellationToken ct)
    {
        var schedule = await _db.BackupSchedules.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return schedule is null ? NotFound() : Ok(ScheduleResponse.From(schedule));
    }

    /// <summary>Enable or disable a schedule.</summary>
    [HttpPost("{id:guid}/toggle")]
    public async Task<ActionResult<ScheduleResponse>> Toggle(Guid id, CancellationToken ct)
    {
        var schedule = await _db.BackupSchedules.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (schedule is null) return NotFound();

        schedule.Enabled = !schedule.Enabled;
        if (schedule.Enabled && CronExpression.TryParse(schedule.CronExpression, CronFormat.Standard, out var cron))
        {
            schedule.NextRunAt = cron.GetNextOccurrence(DateTime.UtcNow);
        }
        await _db.SaveChangesAsync(ct);
        return Ok(ScheduleResponse.From(schedule));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var schedule = await _db.BackupSchedules.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (schedule is null) return NotFound();

        _db.BackupSchedules.Remove(schedule);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
