namespace api.Dtos.Ollama;

public class PushModelOllamaRequestDto
{
    public string Model { get; set; } = string.Empty;

    public bool? Insecure { get; set; }
}
