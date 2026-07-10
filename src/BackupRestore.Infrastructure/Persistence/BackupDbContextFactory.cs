using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BackupRestore.Infrastructure.Persistence;

/// <summary>
/// Enables EF Core CLI tooling (migrations) without needing a running host.
/// Uses a placeholder connection string; the actual runtime connection comes
/// from configuration.
/// </summary>
public class BackupDbContextFactory : IDesignTimeDbContextFactory<BackupDbContext>
{
    public BackupDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=backup;Username=backup;Password=backup";

        var options = new DbContextOptionsBuilder<BackupDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new BackupDbContext(options);
    }
}
