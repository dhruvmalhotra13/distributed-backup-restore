using BackupRestore.Api.Hubs;
using BackupRestore.Api.Services;
using BackupRestore.Infrastructure;
using BackupRestore.Infrastructure.Messaging;
using BackupRestore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration)
          .Enrich.FromLogContext()
          .WriteTo.Console());

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMessaging(builder.Configuration);
builder.Services.AddHostedService<ProgressRelayService>();

const string CorsPolicy = "frontend";
builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
    policy.SetIsOriginAllowed(_ => true)
          .AllowAnyHeader()
          .AllowAnyMethod()
          .AllowCredentials()));

var app = builder.Build();

// Apply EF migrations on startup so anyone can just run the stack.
await ApplyMigrationsAsync(app);

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors(CorsPolicy);

app.MapControllers();
app.MapHub<ProgressHub>("/hubs/progress");
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

static async Task ApplyMigrationsAsync(WebApplication app)
{
    const int maxAttempts = 10;
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BackupDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            await db.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied.");
            return;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            logger.LogWarning(ex, "Database not ready (attempt {Attempt}/{Max}); retrying...", attempt, maxAttempts);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
}

// Exposed for integration testing.
public partial class Program { }
