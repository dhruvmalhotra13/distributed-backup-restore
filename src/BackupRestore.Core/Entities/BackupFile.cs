namespace BackupRestore.Core.Entities;

/// <summary>
/// A single file captured by a backup job.
/// </summary>
public class BackupFile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BackupJobId { get; set; }

    /// <summary>Vault backup identifier this file belongs to.</summary>
    public string BackupId { get; set; } = string.Empty;

    /// <summary>Path relative to the source root, using forward slashes.</summary>
    public string RelativePath { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    /// <summary>SHA-256 hash of the full original file, set once fully backed up.</summary>
    public string? FileHash { get; set; }

    public int ChunkCount { get; set; }

    public BackupJob? BackupJob { get; set; }

    public List<BackupChunk> Chunks { get; set; } = new();
}
