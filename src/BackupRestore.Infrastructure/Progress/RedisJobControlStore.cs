using BackupRestore.Core.Abstractions;
using BackupRestore.Core.Contracts;
using StackExchange.Redis;

namespace BackupRestore.Infrastructure.Progress;

/// <summary>
/// Redis-backed store of cooperative control signals (pause/resume/cancel).
/// The running job polls this between chunks.
/// </summary>
public class RedisJobControlStore : IJobControlStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(12);

    private readonly IConnectionMultiplexer _redis;

    public RedisJobControlStore(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public Task SetSignalAsync(Guid jobId, JobControlSignal signal, CancellationToken cancellationToken)
        => _redis.GetDatabase().StringSetAsync(Key(jobId), signal.ToString(), Ttl);

    public async Task<JobControlSignal> GetSignalAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var value = await _redis.GetDatabase().StringGetAsync(Key(jobId));
        if (value.IsNullOrEmpty || !Enum.TryParse<JobControlSignal>(value!, out var signal))
        {
            return JobControlSignal.None;
        }

        return signal;
    }

    public Task ClearAsync(Guid jobId, CancellationToken cancellationToken)
        => _redis.GetDatabase().KeyDeleteAsync(Key(jobId));

    private static string Key(Guid jobId) => $"control:{jobId}";
}
