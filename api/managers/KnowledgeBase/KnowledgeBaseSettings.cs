namespace api.Managers.KnowledgeBase;

// Bound from the "KnowledgeBase" section in appsettings.json.
public class KnowledgeBaseSettings
{
    public string EmbeddingModel { get; set; } = "nomic-embed-text";

    // Must match float[N] in vec_chunks (migration AddVectorChunksTable).
    public int EmbeddingDimensions { get; set; } = 768;

    public int ChunkSizeCharacters { get; set; } = 1000;

    public int ChunkOverlapCharacters { get; set; } = 150;

    public long MaxUploadSizeBytes { get; set; } = 250 * 1024 * 1024;

    public int DefaultTopK { get; set; } = 5;

    public KnowledgeBaseOcrSettings Ocr { get; set; } = new();
}
