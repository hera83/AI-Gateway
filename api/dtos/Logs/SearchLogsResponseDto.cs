namespace api.Dtos.Logs;

public class SearchLogsResponseDto
{
    public int TotalCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }

    public List<LogEntryDto> Items { get; set; } = [];
}
