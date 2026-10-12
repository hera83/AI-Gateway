namespace api.Data.Models;

public class KnowledgeChunk : EntityBase
{
    public Guid ApiKeyId { get; set; }

    public Guid KnowledgeDocumentId { get; set; }

    public int ChunkIndex { get; set; }

    public string Text { get; set; } = string.Empty;

    // rowid of the matching row in vectors.vec_chunks (see repositories/KnowledgeDocuments).
    public long VectorRowId { get; set; }
}
