namespace api.Data.Models;

public class KnowledgeDocument : EntityBase
{
    public Guid ApiKeyId { get; set; }

    public Guid KnowledgeGroupId { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string? ExtractedText { get; set; }

    // The same content laid out as Markdown (headings, lists, tables). Set together with ExtractedText.
    public string? MarkdownText { get; set; }

    // null = the Markdown is missing or was made before sources were tracked, so the background worker makes it again.
    public KnowledgeMarkdownSource? MarkdownSource { get; set; }

    // MarkdownChunker.Version the chunks (and vectors) were made with. 0 = chunks of the raw text.
    public int IndexVersion { get; set; }

    // MarkdownFormatter.Version the Markdown was made with. Older versions are made again in the background.
    public int MarkdownVersion { get; set; }

    // LLM layout in progress (MarkdownSource LlmPending): the parts finished so far, so a restart continues where
    // it stopped instead of starting over. null when no layout is running.
    public string? MarkdownLlmDraft { get; set; }

    public int? MarkdownLlmPartsDone { get; set; }

    public int? MarkdownLlmPartsTotal { get; set; }

    public KnowledgeDocumentStatus Status { get; set; }

    public string? ErrorMessage { get; set; }
}
