using api.Data.Models;
using api.Repositories.KnowledgeDocuments.Dtos;

namespace api.Repositories.KnowledgeDocuments.Mappers;

public static class KnowledgeChunkMapper
{
    public static KnowledgeChunk ToEntity(this CreateKnowledgeChunkDto dto, KnowledgeDocument document, int chunkIndex, long vectorRowId) => new()
    {
        ApiKeyId = document.ApiKeyId,
        KnowledgeDocumentId = document.Id,
        ChunkIndex = chunkIndex,
        Text = dto.Text,
        VectorRowId = vectorRowId
    };

    public static KnowledgeChunkMatchDto ToMatchDto(
        Guid chunkId, Guid knowledgeDocumentId, string originalFileName, Guid knowledgeGroupId, string text, double distance) => new()
    {
        ChunkId = chunkId,
        KnowledgeDocumentId = knowledgeDocumentId,
        OriginalFileName = originalFileName,
        KnowledgeGroupId = knowledgeGroupId,
        Text = text,
        // vec_chunks uses distance_metric=cosine, so distance is 1 - cosine similarity.
        Score = 1 - distance
    };
}
