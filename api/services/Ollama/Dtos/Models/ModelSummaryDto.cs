namespace api.Services.Ollama.Dtos.Models;

public class ModelSummaryDto
{
    public string Name { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public DateTimeOffset? ModifiedAt { get; set; }

    public long Size { get; set; }

    public string? Digest { get; set; }

    public ModelDetailsDto? Details { get; set; }
}
