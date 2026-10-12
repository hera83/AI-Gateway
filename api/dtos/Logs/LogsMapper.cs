using api.Extensions;
using api.Repositories.Logs.Dtos;

namespace api.Dtos.Logs;

public static class LogsMapper
{
    public static SearchLogsDto ToSearchLogsDto(this SearchLogsRequestDto request) => new()
    {
        Level = request.Level,
        Search = request.Search,
        From = request.From.AsUtc(),
        To = request.To.AsUtc(),
        Page = request.Page,
        PageSize = request.PageSize
    };

    public static SearchLogsResponseDto ToSearchLogsResponseDto(this LogPageDto page) => new()
    {
        TotalCount = page.TotalCount,
        Page = page.Page,
        PageSize = page.PageSize,
        Items = page.Items.Select(ToLogEntryDto).ToList()
    };

    public static LogEntryDto ToLogEntryDto(this LogDto log) => new()
    {
        Id = log.Id,
        Timestamp = log.Timestamp,
        Level = log.Level,
        Message = log.Message,
        Exception = log.Exception,
        Properties = log.Properties
    };
}
