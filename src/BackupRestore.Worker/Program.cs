using BackupRestore.Infrastructure;
using BackupRestore.Infrastructure.Messaging;
using BackupRestore.Worker.Consumers;
using BackupRestore.Worker.Services;
using MassTransit;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, config) =>
    config.ReadFrom.Configuration(builder.Configuration)
          .Enrich.FromLogContext()
          .WriteTo.Console());

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<BackupProcessor>();
builder.Services.AddScoped<RestoreProcessor>();

builder.Services.AddMessaging(builder.Configuration, x =>
{
    x.AddConsumer<BackupRequestedConsumer>();
    x.AddConsumer<RestoreRequestedConsumer>();
});

var host = builder.Build();
host.Run();
