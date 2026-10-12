using api.Infrastructure.Logging;
using Microsoft.Data.Sqlite;

namespace api.BgWorkers;

// Size cap for log.db (the Serilog sink runs with rollOver: false). Not a time-based retention policy.
public class LogCleanupWorker(LogDatabaseSettings settings, ILogger<LogCleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private const double DeleteFraction = 0.10;
    private const int MinRowsPerPass = 1000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            do
            {
                try
                {
                    await CleanupAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Expected on shutdown.
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "{Worker} failed", nameof(LogCleanupWorker));
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // WaitForNextTickAsync throws when stoppingToken is cancelled.
        }
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        if (GetDatabaseSize() <= settings.MaxSizeBytes)
        {
            return;
        }

        await using var connection = new SqliteConnection(settings.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        while (GetDatabaseSize() > settings.MaxSizeBytes)
        {
            var rowCount = await ExecuteScalarAsync(connection, $"SELECT COUNT(*) FROM {settings.TableName};", cancellationToken);
            if (rowCount == 0)
            {
                break;
            }

            var toDelete = Math.Max(MinRowsPerPass, (long)(rowCount * DeleteFraction));
            await ExecuteAsync(connection, $"DELETE FROM {settings.TableName} WHERE id IN (SELECT id FROM {settings.TableName} ORDER BY id LIMIT {toDelete});", cancellationToken);
            await ExecuteAsync(connection, "VACUUM;", cancellationToken);
            await ExecuteAsync(connection, "PRAGMA wal_checkpoint(TRUNCATE);", cancellationToken);

            logger.LogWarning("Deleted the oldest {Count} log rows because log.db exceeded {MaxSizeBytes} bytes", toDelete, settings.MaxSizeBytes);
        }
    }

    private long GetDatabaseSize()
    {
        var walPath = settings.DatabasePath + "-wal";
        var size = File.Exists(settings.DatabasePath) ? new FileInfo(settings.DatabasePath).Length : 0;
        return size + (File.Exists(walPath) ? new FileInfo(walPath).Length : 0);
    }

    private static async Task<long> ExecuteScalarAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
