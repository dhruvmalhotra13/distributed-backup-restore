using System.ComponentModel.DataAnnotations;

namespace BackupRestore.Api.Dtos;

public record CreateBackupJobRequest
{
    [Required]
    public string SourcePath { get; init; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string BackupName { get; init; } = string.Empty;

    /// <summary>Optional override for chunk size in bytes; defaults to server config.</summary>
    [Range(64 * 1024, 256 * 1024 * 1024)]
    public int? ChunkSizeBytes { get; init; }
}

public record CreateRestoreJobRequest
{
    [Required]
    public string BackupId { get; init; } = string.Empty;

    [Required]
    public string RestorePath { get; init; } = string.Empty;
}
