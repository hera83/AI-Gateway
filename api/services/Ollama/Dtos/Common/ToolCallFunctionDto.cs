using System.Text.Json;

namespace api.Services.Ollama.Dtos.Common;

public class ToolCallFunctionDto
{
    public string Name { get; set; } = string.Empty;

    public Dictionary<string, JsonElement> Arguments { get; set; } = [];
}
