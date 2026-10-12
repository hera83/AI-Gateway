namespace api.Services.Ollama.Dtos.Models;

public class CopyModelRequestDto
{
    public string Source { get; set; } = string.Empty;

    public string Destination { get; set; } = string.Empty;
}
