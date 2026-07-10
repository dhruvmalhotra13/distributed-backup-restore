namespace BackupRestore.Core;

/// <summary>
/// Pure helpers for byte-based progress calculation. Progress is based on bytes
/// (not file count) because one large file matters more than many tiny ones.
/// </summary>
public static class ProgressCalculator
{
    /// <summary>Returns a 0..100 percentage, clamped and safe for zero totals.</summary>
    public static double Percent(long processedBytes, long totalBytes)
    {
        if (totalBytes <= 0)
        {
            return processedBytes > 0 ? 100d : 0d;
        }

        var pct = (double)processedBytes / totalBytes * 100d;
        if (pct < 0d) return 0d;
        if (pct > 100d) return 100d;
        return pct;
    }

    /// <summary>
    /// Enforces monotonic progress: never returns a value lower than what was
    /// already committed for the job.
    /// </summary>
    public static long Monotonic(long previouslyCommittedBytes, long candidateBytes)
        => Math.Max(previouslyCommittedBytes, candidateBytes);
}
