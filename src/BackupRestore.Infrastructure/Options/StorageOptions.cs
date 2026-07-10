namespace BackupRestore.Infrastructure.Options;

public class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Absolute path to the Backup Vault root inside the container/host.</summary>
    public string VaultPath { get; set; } = "/data/BackupVault";

    /// <summary>Default chunk size in bytes (4 MB by default).</summary>
    public int ChunkSizeBytes { get; set; } = 4 * 1024 * 1024;
}
