namespace BackupRestore.Core;

/// <summary>
/// Generates short, human-friendly, filesystem-safe backup identifiers used as
/// vault folder names, e.g. "backup-a1b2c3d4".
/// </summary>
public static class BackupIdFactory
{
    public static string NewId()
        => $"backup-{Guid.NewGuid().ToString("N")[..8]}";
}
