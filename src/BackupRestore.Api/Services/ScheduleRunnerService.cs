using BackupRestore.Infrastructure.Persistence;
using Cronos;
using Microsoft.EntityFrameworkCore;

namespace BackupRestore.Api.Services;

/// <summary>
/// Evaluates backup schedules on a fixed cadence and queues a backup job for any
/// schedule whose next occurrence is due, then computes the following occurrence.
/// </summary>
public class ScheduleRunnerService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(20);

    private readonly IServiceProvider _services;
    private readonly ILogger<ScheduleRunnerService> _logger;

    public ScheduleRunnerService(IServiceProvider services, ILogger<ScheduleRunnerService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunDueSchedulesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Schedule runner tick failed.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunDueSchedulesAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BackupDbContext>();
        var jobs = scope.ServiceProvider.GetRequiredService<BackupJobService>();

        var now = DateTime.UtcNow;
        var due = await db.BackupSchedules
            .Where(s => s.Enabled && s.NextRunAt != null && s.NextRunAt <= now)
            .ToListAsync(ct);

        foreach (var schedule in due)
        {
            if (!Directory.Exists(schedule.SourcePath))
            {
                _logger.LogWarning("Schedule '{Name}' source missing: {Path}; skipping.", schedule.Name, schedule.SourcePath);
            }
            else
            {
                await jobs.CreateAndQueueAsync(schedule.SourcePath, schedule.Name, null, "scheduled", ct);
                _logger.LogInformation("Scheduled backup queued for '{Name}'.", schedule.Name);
            }

            schedule.LastRunAt = now;
            schedule.NextRunAt = CronExpression.TryParse(schedule.CronExpression, CronFormat.Standard, out var cron)
                ? cron.GetNextOccurrence(now)
                : null;
        }

        if (due.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }
    }
}
