using System.Text.Json;

namespace api.Dtos.Ollama;

public class ChatOllamaRequestDto
{
    public string Model { get; set; } = string.Empty;

    public List<MessageDto> Messages { get; set; } = [];

    public List<ToolDto>? Tools { get; set; }

    public JsonElement? Format { get; set; }

    public OptionsDto? Options { get; set; }

    public string? KeepAlive { get; set; }

    // Defaults to false (unlike Ollama's own default of true) so existing callers that don't set
    // this keep getting today's single-JSON-object response instead of suddenly switching shape.
    public bool Stream { get; set; }
}
