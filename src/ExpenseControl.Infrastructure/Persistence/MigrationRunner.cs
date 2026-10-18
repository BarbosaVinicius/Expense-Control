using System.Reflection;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace ExpenseControl.Infrastructure.Persistence;

/// <summary>
/// Applies the embedded SQL scripts (<c>Persistence/Migrations/*.sql</c>) that have not run yet, in file-name order,
/// and records them in the <c>SchemaVersions</c> table. To change the schema, add a new, higher-numbered script;
/// never edit one that has already been released.
/// </summary>
internal sealed class MigrationRunner(MySqlDataSource dataSource, string connectionString, ILogger<MigrationRunner> logger)
{
    private const string LockName = "expense_control_migrations";
    private const string ResourceMarker = ".Migrations.";

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await EnsureDatabaseExistsAsync(cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        // Prevents two instances starting at the same time from applying the same script twice.
        await using (var lockCommand = new MySqlCommand("SELECT GET_LOCK(@name, 60)", connection))
        {
            lockCommand.Parameters.AddWithValue("@name", LockName);
            if (Convert.ToInt32(await lockCommand.ExecuteScalarAsync(cancellationToken)) != 1)
            {
                throw new InvalidOperationException("Could not acquire the database migration lock.");
            }
        }

        try
        {
            await ExecuteAsync(connection, """
                CREATE TABLE IF NOT EXISTS SchemaVersions (
                    Version VARCHAR(100) NOT NULL PRIMARY KEY,
                    AppliedAtUtc DATETIME NOT NULL
                )
                """, cancellationToken);

            var applied = await GetAppliedVersionsAsync(connection, cancellationToken);

            foreach (var script in DiscoverScripts())
            {
                if (applied.Contains(script.Version))
                {
                    continue;
                }

                logger.LogInformation("Applying database migration {Version}.", script.Version);
                await ExecuteAsync(connection, script.Sql, cancellationToken);

                await using var record = new MySqlCommand(
                    "INSERT INTO SchemaVersions (Version, AppliedAtUtc) VALUES (@version, UTC_TIMESTAMP())", connection);
                record.Parameters.AddWithValue("@version", script.Version);
                await record.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        finally
        {
            await using var release = new MySqlCommand("SELECT RELEASE_LOCK(@name)", connection);
            release.Parameters.AddWithValue("@name", LockName);
            await release.ExecuteScalarAsync(CancellationToken.None);
        }
    }

    /// <summary>The embedded migration scripts, ordered by version.</summary>
    internal static IReadOnlyList<MigrationScript> DiscoverScripts()
    {
        var assembly = typeof(MigrationRunner).Assembly;
        var scripts = new List<MigrationScript>();

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            var markerIndex = resourceName.IndexOf(ResourceMarker, StringComparison.Ordinal);
            if (markerIndex < 0 || !resourceName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var version = resourceName[(markerIndex + ResourceMarker.Length)..^".sql".Length];
            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream);
            scripts.Add(new MigrationScript(version, reader.ReadToEnd()));
        }

        return scripts.OrderBy(s => s.Version, StringComparer.Ordinal).ToList();
    }

    private async Task EnsureDatabaseExistsAsync(CancellationToken cancellationToken)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString);
        var database = builder.Database;
        if (string.IsNullOrEmpty(database))
        {
            return;
        }

        builder.Database = string.Empty;
        builder.Pooling = false;

        try
        {
            await using var connection = new MySqlConnection(builder.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await ExecuteAsync(connection, $"CREATE DATABASE IF NOT EXISTS `{database.Replace("`", "``")}`", cancellationToken);
        }
        catch (MySqlException ex)
        {
            // Typically a missing CREATE privilege; if the database really is missing, the next step fails with a clear error.
            logger.LogWarning(ex, "Could not verify that database {Database} exists.", database);
        }
    }

    private static async Task<HashSet<string>> GetAppliedVersionsAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        var versions = new HashSet<string>(StringComparer.Ordinal);
        await using var command = new MySqlCommand("SELECT Version FROM SchemaVersions", connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            versions.Add(reader.GetString(0));
        }

        return versions;
    }

    private static async Task ExecuteAsync(MySqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal sealed record MigrationScript(string Version, string Sql);
}
