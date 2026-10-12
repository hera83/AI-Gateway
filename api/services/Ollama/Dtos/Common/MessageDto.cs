namespace api.Services.Ollama.Dtos.Common;

public class MessageDto
{
    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public List<string>? Images { get; set; }

    public List<ToolCallDto>? ToolCalls { get; set; }
}
