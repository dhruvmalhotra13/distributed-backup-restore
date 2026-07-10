using BackupRestore.Core.Entities;

namespace BackupRestore.Api.Dtos;

public record BackupJobResponse
{
    public Guid Id { get; init; }
    public string BackupId { get; init; } = string.Empty;
    public string BackupName { get; init; } = string.Empty;
    public string SourcePath { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int Version { get; init; }
    public long TotalBytes { get; init; }
    public long CopiedBytes { get; init; }
    public long DedupedBytes { get; init; }
    public long StoredBytes { get; init; }
    public int TotalFiles { get; init; }
    public int FilesProcessed { get; init; }
    public double ProgressPercent { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }

    public static BackupJobResponse From(BackupJob job) => new()
    {
        Id = job.Id,
        BackupId = job.BackupId,
        BackupName = job.BackupName,
        SourcePath = job.SourcePath,
        Status = job.Status.ToString(),
        Version = job.Version,
        TotalBytes = job.TotalBytes,
        CopiedBytes = job.CopiedBytes,
        DedupedBytes = job.DedupedBytes,
        StoredBytes = job.TotalBytes - job.DedupedBytes,
        TotalFiles = job.TotalFiles,
        FilesProcessed = job.FilesProcessed,
        ProgressPercent = job.ProgressPercent,
        ErrorMessage = job.ErrorMessage,
        CreatedAt = job.CreatedAt,
        UpdatedAt = job.UpdatedAt
    };
}

public record RestoreJobResponse
{
    public Guid Id { get; init; }
    public string BackupId { get; init; } = string.Empty;
    public string RestorePath { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public long TotalBytes { get; init; }
    public long RestoredBytes { get; init; }
    public double ProgressPercent { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }

    public static RestoreJobResponse From(RestoreJob job) => new()
    {
        Id = job.Id,
        BackupId = job.BackupId,
        RestorePath = job.RestorePath,
        Status = job.Status.ToString(),
        TotalBytes = job.TotalBytes,
        RestoredBytes = job.RestoredBytes,
        ProgressPercent = job.ProgressPercent,
        ErrorMessage = job.ErrorMessage,
        CreatedAt = job.CreatedAt,
        UpdatedAt = job.UpdatedAt
    };
}

public record JobEventResponse
{
    public Guid Id { get; init; }
    public Guid JobId { get; init; }
    public string JobType { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }

    public static JobEventResponse From(JobEvent e) => new()
    {
        Id = e.Id,
        JobId = e.JobId,
        JobType = e.JobType,
        EventType = e.EventType,
        Message = e.Message,
        Timestamp = e.Timestamp
    };
}

public record ScheduleResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string SourcePath { get; init; } = string.Empty;
    public string CronExpression { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public DateTime? LastRunAt { get; init; }
    public DateTime? NextRunAt { get; init; }
    public DateTime CreatedAt { get; init; }

    public static ScheduleResponse From(BackupSchedule s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        SourcePath = s.SourcePath,
        CronExpression = s.CronExpression,
        Enabled = s.Enabled,
        LastRunAt = s.LastRunAt,
        NextRunAt = s.NextRunAt,
        CreatedAt = s.CreatedAt
    };
}
