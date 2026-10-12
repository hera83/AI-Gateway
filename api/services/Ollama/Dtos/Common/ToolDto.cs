namespace api.Services.Ollama.Dtos.Common;

public class ToolDto
{
    public string Type { get; set; } = "function";

    public ToolFunctionDto Function { get; set; } = new();
}
