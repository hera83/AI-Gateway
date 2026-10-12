namespace api.Dtos.Ollama;

public class CopyModelOllamaRequestDto
{
    public string Source { get; set; } = string.Empty;

    public string Destination { get; set; } = string.Empty;
}
