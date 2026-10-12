using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace api.Infrastructure.Database;

// SQLite stores DateTime as text without a kind, so values read back would be DateTimeKind.Unspecified and
// serialize without "Z". Everything is stored as UTC, so it is marked as UTC on read.
public class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime(),
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
