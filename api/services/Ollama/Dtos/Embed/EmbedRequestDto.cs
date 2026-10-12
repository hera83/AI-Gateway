using api.Services.Ollama.Dtos.Common;

namespace api.Services.Ollama.Dtos.Embed;

public class EmbedRequestDto
{
    public string Model { get; set; } = string.Empty;

    public List<string> Input { get; set; } = [];

    public OptionsDto? Options { get; set; }

    public string? KeepAlive { get; set; }
}
