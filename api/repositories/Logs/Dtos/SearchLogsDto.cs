namespace api.Repositories.Logs.Dtos;

public class SearchLogsDto
{
    public string? Level { get; set; }

    public string? Search { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}
