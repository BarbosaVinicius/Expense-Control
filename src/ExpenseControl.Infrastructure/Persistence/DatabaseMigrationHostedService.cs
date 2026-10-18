using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExpenseControl.Infrastructure.Persistence;

/// <summary>Brings the database schema up to date before the application starts serving requests.</summary>
internal sealed class DatabaseMigrationHostedService(
    MigrationRunner runner,
    IOptions<DatabaseOptions> options,
    ILogger<DatabaseMigrationHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.MigrateOnStartup)
        {
            logger.LogInformation("Database migrations on startup are disabled.");
            return;
        }

        try
        {
            await runner.RunAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogCritical(ex, "Could not prepare the database. Check ConnectionStrings:DefaultConnection and that MySQL is running.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
