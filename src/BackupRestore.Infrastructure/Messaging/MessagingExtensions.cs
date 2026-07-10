using System;
using BackupRestore.Infrastructure.Options;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BackupRestore.Infrastructure.Messaging;

public static class MessagingExtensions
{
    /// <summary>
    /// Configures MassTransit over RabbitMQ. Hosts pass an optional action to
    /// register consumers (the worker) or none at all (the API, publish-only).
    /// </summary>
    public static IServiceCollection AddMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
    {
        services.AddMassTransit(x =>
        {
            configureConsumers?.Invoke(x);

            x.UsingRabbitMq((context, cfg) =>
            {
                var options = configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>()
                    ?? new RabbitMqOptions();

                cfg.Host(options.Host, options.Port, options.VirtualHost, h =>
                {
                    h.Username(options.Username);
                    h.Password(options.Password);
                });

                cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
