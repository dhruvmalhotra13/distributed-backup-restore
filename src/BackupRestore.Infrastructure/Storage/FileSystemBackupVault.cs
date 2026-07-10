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

    public async Task WriteChunkAsync(string vaultRelativePath, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        var absolute = ResolveAbsolute(vaultRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);

        await using var stream = new FileStream(
            absolute, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 1 << 16, useAsync: true);
        await stream.WriteAsync(data, cancellationToken);
        await stream.FlushAsync(cancellationToken);
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
