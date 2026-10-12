using System.Text.Json;

namespace api.Services.Ollama.Dtos.Common;

public class ToolFunctionDto
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public JsonElement Parameters { get; set; }
}
