namespace api.Infrastructure.Logging;

// Single source of truth for log.db, shared by the Serilog sink, LogRepository and LogCleanupWorker.
public class LogDatabaseSettings(string databasePath)
{
    public string DatabasePath { get; } = databasePath;

    public string TableName { get; } = "Logs";

    public long MaxSizeBytes { get; } = 1024L * 1024 * 1024;

    public string ConnectionString => $"Data Source={DatabasePath}";
}
