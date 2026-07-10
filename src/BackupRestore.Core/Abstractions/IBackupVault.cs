namespace BackupRestore.Core.Abstractions;

/// <summary>
/// Storage abstraction for the local Backup Vault. Pure file IO; hashing and
/// orchestration live in the worker processors.
/// </summary>
public interface IBackupVault
{
    /// <summary>Vault-relative path where a given chunk is stored.</summary>
    string GetChunkPath(string backupId, Guid fileId, int chunkIndex);

    /// <summary>Writes chunk bytes to the vault (creating directories as needed).</summary>
    Task WriteChunkAsync(string vaultRelativePath, ReadOnlyMemory<byte> data, CancellationToken cancellationToken);

    /// <summary>Opens a chunk for reading. Caller disposes the stream.</summary>
    Stream OpenChunkRead(string vaultRelativePath);

    /// <summary>True if the chunk already exists (used for idempotent resume).</summary>
    bool ChunkExists(string vaultRelativePath);

    /// <summary>Writes a text/JSON artifact (metadata.json, manifest.json, hashes) under the backup folder.</summary>
    Task WriteTextAsync(string backupId, string relativeName, string content, CancellationToken cancellationToken);

    /// <summary>Appends a line to the backup's worker.log.</summary>
    Task AppendLogAsync(string backupId, string line, CancellationToken cancellationToken);
}
