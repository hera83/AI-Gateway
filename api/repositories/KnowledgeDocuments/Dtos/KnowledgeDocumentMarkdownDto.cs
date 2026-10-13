namespace api.Repositories.KnowledgeDocuments.Dtos;

public class KnowledgeDocumentMarkdownDto
{
    public Guid Id { get; set; }

    public Guid KnowledgeGroupId { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    // null until the document is Indexed (and, for documents indexed before Markdown existed, until the
    // background worker has made it).
    public string? MarkdownText { get; set; }

    // Structure, LlmPending or Llm. null while MarkdownText is null.
    public string? MarkdownSource { get; set; }

    // How far the LLM layout has got while MarkdownSource is LlmPending (null before it starts).
    public int? LlmPartsDone { get; set; }

    public int? LlmPartsTotal { get; set; }
}
