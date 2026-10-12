namespace api.Dtos.KnowledgeBase;

public class SearchKnowledgeBaseResponseDto
{
    public Guid ChunkId { get; set; }

    public Guid DocumentId { get; set; }

    public string DocumentFileName { get; set; } = string.Empty;

    public Guid GroupId { get; set; }

    public string Text { get; set; } = string.Empty;

    // Higher is more relevant (1 - cosine distance).
    public double Score { get; set; }
}
