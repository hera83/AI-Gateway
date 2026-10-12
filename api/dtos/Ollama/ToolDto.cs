namespace api.Dtos.Ollama;

public class ToolDto
{
    public string Type { get; set; } = "function";

    public ToolFunctionDto Function { get; set; } = new();
}
