using System.Text.Json;

namespace api.Dtos.Ollama;

public class ToolCallFunctionDto
{
    public string Name { get; set; } = string.Empty;

    public Dictionary<string, JsonElement> Arguments { get; set; } = [];
}
