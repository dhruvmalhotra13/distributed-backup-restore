using BackupRestore.Core.Abstractions;
using BackupRestore.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace BackupRestore.Infrastructure.Storage;

/// <summary>
/// Local file-system implementation of the Backup Vault. Layout:
/// <code>
/// {VaultPath}/{backupId}/chunks/{fileId}/chunk-000001.bin
/// {VaultPath}/{backupId}/metadata.json
/// {VaultPath}/{backupId}/manifest.json
/// {VaultPath}/{backupId}/logs/worker.log
/// </code>
/// </summary>
public class FileSystemBackupVault : IBackupVault
{
    private readonly string _vaultRoot;

    public FileSystemBackupVault(IOptions<StorageOptions> options)
    {
        _vaultRoot = options.Value.VaultPath;
        Directory.CreateDirectory(_vaultRoot);
    }

    public string GetChunkPath(string backupId, Guid fileId, int chunkIndex)
        => $"{backupId}/chunks/{fileId}/chunk-{chunkIndex:D6}.bin";

    // Content-addressed store shared by all backups: _cas/{first2}/{hash}.bin
    public string GetCasChunkPath(string chunkHash)
        => $"_cas/{chunkHash[..2]}/{chunkHash}.bin";

    public async Task WriteChunkAsync(string vaultRelativePath, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        var absolute = ResolveAbsolute(vaultRelativePath);
        var directory = Path.GetDirectoryName(absolute)!;
        Directory.CreateDirectory(directory);

        // Content-addressed chunks are immutable: if it already exists, the bytes
        // are identical, so there is nothing to write.
        if (File.Exists(absolute))
        {
            return;
        }

        // Write to a unique temp file, then atomically publish it. This keeps
        // concurrent writers of the same chunk (dedup / scaled workers) safe and
        // guarantees readers never observe a partially-written chunk.
        var temp = Path.Combine(directory, Path.GetRandomFileName() + ".tmp");
        try
        {
            await using (var stream = new FileStream(
                temp, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 1 << 16, useAsync: true))
            {
                await stream.WriteAsync(data, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            try
            {
                File.Move(temp, absolute);
            }
            catch (IOException) when (File.Exists(absolute))
            {
                // Another writer published the same chunk first; keep theirs.
            }
        }
        finally
        {
            if (File.Exists(temp))
            {
                try { File.Delete(temp); } catch { /* best effort */ }
            }
        }
    }

    public Stream OpenChunkRead(string vaultRelativePath)
    {
        var absolute = ResolveAbsolute(vaultRelativePath);
        return new FileStream(
            absolute, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 1 << 16, useAsync: true);
    }

    public bool ChunkExists(string vaultRelativePath)
        => File.Exists(ResolveAbsolute(vaultRelativePath));

    public async Task WriteTextAsync(string backupId, string relativeName, string content, CancellationToken cancellationToken)
    {
        var absolute = Path.Combine(_vaultRoot, backupId, relativeName);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        await File.WriteAllTextAsync(absolute, content, cancellationToken);
    }

    public async Task AppendLogAsync(string backupId, string line, CancellationToken cancellationToken)
    {
        var absolute = Path.Combine(_vaultRoot, backupId, "logs", "worker.log");
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        var stamped = $"{DateTime.UtcNow:O} {line}{Environment.NewLine}";
        await File.AppendAllTextAsync(absolute, stamped, cancellationToken);
    }

    private string ResolveAbsolute(string vaultRelativePath)
    {
        var normalized = vaultRelativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(_vaultRoot, normalized);
    }
}
