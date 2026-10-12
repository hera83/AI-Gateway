namespace api.Repositories.Logs.Dtos;

public class LogPageDto
{
    public int TotalCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }

    public List<LogDto> Items { get; set; } = [];
}
