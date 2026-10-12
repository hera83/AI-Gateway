namespace api.Repositories.KnowledgeDocuments.Dtos;

public class IndexKnowledgeDocumentDto
{
    public string ExtractedText { get; set; } = string.Empty;

    public List<CreateKnowledgeChunkDto> Chunks { get; set; } = [];
}
