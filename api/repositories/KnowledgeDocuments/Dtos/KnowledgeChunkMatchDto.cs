namespace api.Repositories.KnowledgeDocuments.Dtos;

public class KnowledgeChunkMatchDto
{
    public Guid ChunkId { get; set; }

    public Guid KnowledgeDocumentId { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public Guid KnowledgeGroupId { get; set; }

    public string Text { get; set; } = string.Empty;

    // 1 - cosine distance, so higher is more relevant.
    public double Score { get; set; }
}
