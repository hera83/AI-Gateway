using System.Globalization;
using api.Infrastructure.Logging;
using api.Repositories.Logs.Dtos;
using api.Repositories.Logs.Interfaces;
using api.Repositories.Logs.Mappers;
using Microsoft.Data.Sqlite;

namespace api.Repositories.Logs;

// Read-only, parameterized ADO.NET queries against log.db. The table schema is owned by
// Serilog.Sinks.SQLite, so there is no EF entity or migration for it.
public class LogRepository(LogDatabaseSettings settings) : ILogRepository
{
    private const int MaxPageSize = 500;

    // The format the sink stores timestamps in (storeTimestampInUtc: true), so string ranges are sargable.
    private const string TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fff";

    public async Task<LogPageDto> SearchAsync(SearchLogsDto query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        await using var connection = new SqliteConnection(settings.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var conditions = new List<string>();
        var parameters = new List<(string Name, object Value)>();

        if (!string.IsNullOrWhiteSpace(query.Level))
        {
            conditions.Add("Level = @level COLLATE NOCASE");
            parameters.Add(("@level", query.Level));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            conditions.Add("(RenderedMessage LIKE @search OR Exception LIKE @search)");
            parameters.Add(("@search", $"%{query.Search}%"));
        }

        if (query.From is { } from)
        {
            conditions.Add("Timestamp >= @from");
            parameters.Add(("@from", from.ToUniversalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture)));
        }

        if (query.To is { } to)
        {
            conditions.Add("Timestamp <= @to");
            parameters.Add(("@to", to.ToUniversalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture)));
        }

        var whereClause = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : string.Empty;

        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = $"SELECT COUNT(*) FROM {settings.TableName} {whereClause}";
        AddParameters(countCommand, parameters);
        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);

        await using var pageCommand = connection.CreateCommand();
        pageCommand.CommandText = $"""
            SELECT {LogMapper.Columns}
            FROM {settings.TableName}
            {whereClause}
            ORDER BY id DESC
            LIMIT @pageSize OFFSET @offset
            """;
        AddParameters(pageCommand, parameters);
        pageCommand.Parameters.AddWithValue("@pageSize", pageSize);
        pageCommand.Parameters.AddWithValue("@offset", (page - 1) * pageSize);

        var items = new List<LogDto>();
        await using var reader = await pageCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(reader.ToDto());
        }

        return new LogPageDto
        {
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            Items = items
        };
    }

    private static void AddParameters(SqliteCommand command, List<(string Name, object Value)> parameters)
    {
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }
    }
}
