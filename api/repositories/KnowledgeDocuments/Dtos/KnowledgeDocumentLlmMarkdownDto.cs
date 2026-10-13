namespace api.Repositories.KnowledgeDocuments.Dtos;

// A document waiting for LLM layout of its Markdown, with how far an earlier run got.
public class KnowledgeDocumentLlmMarkdownDto
{
    public Guid Id { get; set; }

    // The Markdown made from the structure: the input for the LLM.
    public string MarkdownText { get; set; } = string.Empty;

    public string? Draft { get; set; }

    public int? PartsDone { get; set; }

    public int? PartsTotal { get; set; }
}
