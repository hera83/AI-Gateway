namespace api.Dtos.Ollama;

public class EmbedOllamaResponseDto
{
    public string Model { get; set; } = string.Empty;

    public List<List<double>> Embeddings { get; set; } = [];

    public long? TotalDuration { get; set; }

    public long? LoadDuration { get; set; }

    public int? PromptEvalCount { get; set; }
}
