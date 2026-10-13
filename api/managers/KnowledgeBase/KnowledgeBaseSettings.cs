namespace api.Managers.KnowledgeBase;

// Bound from the "KnowledgeBase" section in appsettings.json.
public class KnowledgeBaseSettings
{
    public string EmbeddingModel { get; set; } = "nomic-embed-text";

    // Must match float[N] in vec_chunks (migration AddVectorChunksTable).
    public int EmbeddingDimensions { get; set; } = 768;

    public int ChunkSizeCharacters { get; set; } = 1000;

    // Chunks follow the Markdown's paragraphs (MarkdownChunker), so they only overlap where a paragraph longer than
    // ChunkSizeCharacters is split, or where a document without Markdown falls back to fixed windows.
    public int ChunkOverlapCharacters { get; set; } = 150;

    public long MaxUploadSizeBytes { get; set; } = 250 * 1024 * 1024;

    public int DefaultTopK { get; set; } = 5;

    // Fallback check for waiting uploads in KnowledgeDocumentProcessingWorker (new uploads wake it at once), and how
    // often KnowledgeMarkdownWorker looks for Markdown to lay out with the LLM.
    public int WorkerIntervalSeconds { get; set; } = 60;

    public KnowledgeBaseOcrSettings Ocr { get; set; } = new();

    public KnowledgeBaseMarkdownSettings Markdown { get; set; } = new();

    public KnowledgeBaseAgentSettings Agent { get; set; } = new();
}
