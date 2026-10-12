namespace api.Services.Ollama.Dtos.Common;

public class ToolCallDto
{
    public ToolCallFunctionDto Function { get; set; } = new();
}
