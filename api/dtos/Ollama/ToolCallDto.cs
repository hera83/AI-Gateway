namespace api.Dtos.Ollama;

public class ToolCallDto
{
    public ToolCallFunctionDto Function { get; set; } = new();
}
