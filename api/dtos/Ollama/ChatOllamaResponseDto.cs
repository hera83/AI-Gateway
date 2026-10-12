namespace api.Dtos.Ollama;

public class ChatOllamaResponseDto
{
    public string Model { get; set; } = string.Empty;

    public DateTimeOffset? CreatedAt { get; set; }

    public MessageDto Message { get; set; } = new();

    public bool Done { get; set; }

    public string? DoneReason { get; set; }

    public long? TotalDuration { get; set; }

    public long? LoadDuration { get; set; }

    public int? PromptEvalCount { get; set; }

    public long? PromptEvalDuration { get; set; }

    public int? EvalCount { get; set; }

    public long? EvalDuration { get; set; }
}
