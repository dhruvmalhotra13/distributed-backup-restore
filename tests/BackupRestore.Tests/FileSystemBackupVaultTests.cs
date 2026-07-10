using System.Text;
using BackupRestore.Core.Abstractions;
using BackupRestore.Infrastructure.Options;
using BackupRestore.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BackupRestore.Tests;

public class FileSystemBackupVaultTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly IBackupVault _vault;

    public FileSystemBackupVaultTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "vault-tests-" + Guid.NewGuid().ToString("N"));
        _vault = new FileSystemBackupVault(Options.Create(new StorageOptions { VaultPath = _tempRoot }));
    }

    [Fact]
    public async Task Chunk_round_trips_through_the_vault()
    {
        var backupId = "backup-test01";
        var fileId = Guid.NewGuid();
        var path = _vault.GetChunkPath(backupId, fileId, 0);
        var payload = Encoding.UTF8.GetBytes("hello vault");

        _vault.ChunkExists(path).Should().BeFalse();
        await _vault.WriteChunkAsync(path, payload, CancellationToken.None);
        _vault.ChunkExists(path).Should().BeTrue();

        await using var stream = _vault.OpenChunkRead(path);
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();

        content.Should().Be("hello vault");
    }

    [Fact]
    public async Task WriteText_and_AppendLog_create_expected_files()
    {
        var backupId = "backup-test02";
        await _vault.WriteTextAsync(backupId, "manifest.json", "{}", CancellationToken.None);
        await _vault.AppendLogAsync(backupId, "line one", CancellationToken.None);

        File.Exists(Path.Combine(_tempRoot, backupId, "manifest.json")).Should().BeTrue();
        File.Exists(Path.Combine(_tempRoot, backupId, "logs", "worker.log")).Should().BeTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
