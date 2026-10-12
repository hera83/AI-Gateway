using System.Text.Json;
using api.Services.Ollama.Dtos.Common;

namespace api.Services.Ollama.Dtos.Chat;

public class ChatRequestDto
{
    public string Model { get; set; } = string.Empty;

    public List<MessageDto> Messages { get; set; } = [];

    public List<ToolDto>? Tools { get; set; }

    public JsonElement? Format { get; set; }

    public OptionsDto? Options { get; set; }

    public string? KeepAlive { get; set; }

    public bool Stream { get; set; }
}
