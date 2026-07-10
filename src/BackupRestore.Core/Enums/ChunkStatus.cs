namespace BackupRestore.Core.Enums;

/// <summary>
/// Status of an individual chunk within a backup file.
/// </summary>
public enum ChunkStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2
}
