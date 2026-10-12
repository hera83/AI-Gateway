namespace api.Services.Ollama.Dtos.Models;

public class PushModelResponseDto
{
    public string Status { get; set; } = string.Empty;

    public string? Error { get; set; }
}
