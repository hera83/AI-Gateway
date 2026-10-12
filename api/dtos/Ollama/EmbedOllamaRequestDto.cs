namespace api.Dtos.Ollama;

public class EmbedOllamaRequestDto
{
    public string Model { get; set; } = string.Empty;

    public List<string> Input { get; set; } = [];

    public OptionsDto? Options { get; set; }

    public string? KeepAlive { get; set; }
}
