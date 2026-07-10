using System.Text.Json;
using BackupRestore.Core.Abstractions;
using BackupRestore.Core.Contracts;
using StackExchange.Redis;

namespace BackupRestore.Infrastructure.Progress;

/// <summary>
/// Publishes progress to a Redis pub/sub channel (consumed by the API's SignalR
/// relay) and caches the latest snapshot per job for a polling fallback.
/// </summary>
public class RedisProgressPublisher : IProgressPublisher
{
    public static readonly RedisChannel Channel = RedisChannel.Literal("backup:progress");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan SnapshotTtl = TimeSpan.FromHours(6);

    private readonly IConnectionMultiplexer _redis;

    public RedisProgressPublisher(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task PublishAsync(ProgressUpdate update, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(update, JsonOptions);
        var db = _redis.GetDatabase();

        await db.StringSetAsync(SnapshotKey(update.JobId), payload, SnapshotTtl);
        await _redis.GetSubscriber().PublishAsync(Channel, payload);
    }

    public async Task<ProgressUpdate?> GetLatestAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var value = await _redis.GetDatabase().StringGetAsync(SnapshotKey(jobId));
        if (value.IsNullOrEmpty)
        {
            return null;
        }

        return JsonSerializer.Deserialize<ProgressUpdate>(value!, JsonOptions);
    }

    private static string SnapshotKey(Guid jobId) => $"progress:latest:{jobId}";
}
