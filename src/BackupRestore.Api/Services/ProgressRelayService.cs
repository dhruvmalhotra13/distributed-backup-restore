using System.Text.Json;
using BackupRestore.Api.Hubs;
using BackupRestore.Core.Contracts;
using BackupRestore.Infrastructure.Progress;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace BackupRestore.Api.Services;

/// <summary>
/// Background service that subscribes to the Redis progress channel and relays
/// each snapshot to SignalR clients (both the job-specific group and all clients).
/// This decouples workers from the UI transport.
/// </summary>
public class ProgressRelayService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _redis;
    private readonly IHubContext<ProgressHub> _hub;
    private readonly ILogger<ProgressRelayService> _logger;

    public ProgressRelayService(
        IConnectionMultiplexer redis,
        IHubContext<ProgressHub> hub,
        ILogger<ProgressRelayService> logger)
    {
        _redis = redis;
        _hub = hub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var subscriber = _redis.GetSubscriber();

        await subscriber.SubscribeAsync(RedisProgressPublisher.Channel, async (_, value) =>
        {
            try
            {
                var update = JsonSerializer.Deserialize<ProgressUpdate>(value!, JsonOptions);
                if (update is null)
                {
                    return;
                }

                await _hub.Clients.Group(update.JobId.ToString())
                    .SendAsync("ProgressUpdated", update);
                await _hub.Clients.All.SendAsync("JobProgress", update);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to relay progress update to SignalR.");
            }
        });

        _logger.LogInformation("Progress relay subscribed to Redis channel.");

        // Keep the service alive until shutdown.
        await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { });
    }
}
