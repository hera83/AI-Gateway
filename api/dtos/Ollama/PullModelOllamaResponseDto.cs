namespace api.Dtos.Ollama;

public class PullModelOllamaResponseDto
{
    public string Status { get; set; } = string.Empty;

    public string? Error { get; set; }
}
