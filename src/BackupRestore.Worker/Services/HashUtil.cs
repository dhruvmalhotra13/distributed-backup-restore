using System.Security.Cryptography;

namespace BackupRestore.Worker.Services;

/// <summary>SHA-256 helpers for chunk and whole-file integrity hashing.</summary>
public static class HashUtil
{
    public static string ComputeHex(ReadOnlySpan<byte> data)
        => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    /// <summary>Streams a file through SHA-256 without loading it fully into memory.</summary>
    public static async Task<string> ComputeFileHexAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 1 << 16, useAsync: true);

        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
