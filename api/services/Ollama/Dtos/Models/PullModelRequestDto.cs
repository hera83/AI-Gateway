namespace api.Services.Ollama.Dtos.Models;

public class PullModelRequestDto
{
    public string Model { get; set; } = string.Empty;

    public bool? Insecure { get; set; }

    public bool Stream { get; set; }
}
