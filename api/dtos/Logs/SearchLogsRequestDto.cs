using System.ComponentModel.DataAnnotations;

namespace api.Dtos.Logs;

public class SearchLogsRequestDto
{
    public string? Level { get; set; }

    public string? Search { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 500)]
    public int PageSize { get; set; } = 50;
}
