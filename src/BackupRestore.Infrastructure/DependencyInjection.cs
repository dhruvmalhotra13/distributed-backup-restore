using BackupRestore.Core.Abstractions;
using BackupRestore.Infrastructure.Options;
using BackupRestore.Infrastructure.Persistence;
using BackupRestore.Infrastructure.Progress;
using BackupRestore.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace BackupRestore.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers persistence (PostgreSQL), storage (Backup Vault), and Redis-backed
    /// progress + control services. MassTransit is configured per-host.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));

        var postgresConnection = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Missing connection string 'Postgres'.");

        services.AddDbContext<BackupDbContext>(options => options.UseNpgsql(postgresConnection));

        var redisConnection = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Missing connection string 'Redis'.");

        services.AddSingleton<IConnectionMultiplexer>(
            _ => ConnectionMultiplexer.Connect(redisConnection));

        services.AddSingleton<IBackupVault, FileSystemBackupVault>();
        services.AddSingleton<IProgressPublisher, RedisProgressPublisher>();
        services.AddSingleton<IJobControlStore, RedisJobControlStore>();

        return services;
    }
}
