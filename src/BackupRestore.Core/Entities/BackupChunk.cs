using BackupRestore.Core.Enums;

namespace BackupRestore.Core.Entities;

/// <summary>
/// A fixed-size chunk of a backup file stored in the vault.
/// </summary>
public class BackupChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BackupFileId { get; set; }

    public int ChunkIndex { get; set; }

    public long ChunkSize { get; set; }

    /// <summary>SHA-256 hash of the chunk bytes for integrity validation.</summary>
    public string? ChunkHash { get; set; }

    /// <summary>Path of the chunk file inside the vault (relative to vault root).</summary>
    public string VaultPath { get; set; } = string.Empty;

    public ChunkStatus Status { get; set; } = ChunkStatus.Pending;

    public BackupFile? BackupFile { get; set; }
}
