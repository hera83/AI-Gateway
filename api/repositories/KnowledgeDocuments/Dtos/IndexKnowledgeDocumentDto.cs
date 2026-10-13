namespace api.Repositories.KnowledgeDocuments.Dtos;

public class IndexKnowledgeDocumentDto
{
    public string ExtractedText { get; set; } = string.Empty;

    public string Markdown { get; set; } = string.Empty;

    // The Markdown is from the structure for now, and the LLM worker should lay it out better.
    public bool MarkdownNeedsLlm { get; set; }

    public int MarkdownVersion { get; set; }

    public int IndexVersion { get; set; }

    public List<CreateKnowledgeChunkDto> Chunks { get; set; } = [];
}
