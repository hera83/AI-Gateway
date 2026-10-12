using System.Text.Json;

namespace api.Dtos.Ollama;

public class ToolFunctionDto
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public JsonElement Parameters { get; set; }
}
