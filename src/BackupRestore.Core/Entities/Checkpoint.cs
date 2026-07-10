namespace BackupRestore.Core.Entities;

/// <summary>
/// Records the last successfully completed chunk for a file so a job can
/// resume after a worker crash instead of restarting from zero.
/// </summary>
public class Checkpoint
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BackupJobId { get; set; }

    public Guid BackupFileId { get; set; }

    public int LastCompletedChunkIndex { get; set; } = -1;

    public long BytesCompleted { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
