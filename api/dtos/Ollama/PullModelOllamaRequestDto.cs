namespace api.Dtos.Ollama;

public class PullModelOllamaRequestDto
{
    public string Model { get; set; } = string.Empty;

    public bool? Insecure { get; set; }
}
