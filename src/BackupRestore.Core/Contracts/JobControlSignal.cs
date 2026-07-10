namespace BackupRestore.Core.Contracts;

/// <summary>
/// Cooperative control signals a running job polls for between chunks.
/// Stored in Redis keyed by job id.
/// </summary>
public enum JobControlSignal
{
    None = 0,
    Pause = 1,
    Resume = 2,
    Cancel = 3
}
