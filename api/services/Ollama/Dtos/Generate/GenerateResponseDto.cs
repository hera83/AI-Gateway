namespace api.Services.Ollama.Dtos.Generate;

public class GenerateResponseDto
{
    public string Model { get; set; } = string.Empty;

    public DateTimeOffset? CreatedAt { get; set; }

    public string Response { get; set; } = string.Empty;

    public bool Done { get; set; }

    public string? DoneReason { get; set; }

    public List<long>? Context { get; set; }

    public long? TotalDuration { get; set; }

    public long? LoadDuration { get; set; }

    public int? PromptEvalCount { get; set; }

    public long? PromptEvalDuration { get; set; }

    public int? EvalCount { get; set; }

    public long? EvalDuration { get; set; }
}
