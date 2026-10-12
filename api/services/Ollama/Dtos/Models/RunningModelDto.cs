namespace api.Services.Ollama.Dtos.Models;

public class RunningModelDto
{
    public string Name { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public long Size { get; set; }

    public string? Digest { get; set; }

    public ModelDetailsDto? Details { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public long SizeVram { get; set; }
}
