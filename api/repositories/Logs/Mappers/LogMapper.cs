using System.Globalization;
using api.Repositories.Logs.Dtos;
using Microsoft.Data.Sqlite;

namespace api.Repositories.Logs.Mappers;

public static class LogMapper
{
    // Column names come from Serilog.Sinks.SQLite's own CREATE TABLE, in the order ToDto reads them.
    public const string Columns = "id, Timestamp, Level, Exception, RenderedMessage, Properties";

    public static LogDto ToDto(this SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        Timestamp = DateTime.Parse(reader.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
        Level = reader.GetString(2),
        Exception = reader.IsDBNull(3) ? null : reader.GetString(3),
        Message = reader.IsDBNull(4) ? null : reader.GetString(4),
        Properties = reader.IsDBNull(5) ? null : reader.GetString(5)
    };
}
