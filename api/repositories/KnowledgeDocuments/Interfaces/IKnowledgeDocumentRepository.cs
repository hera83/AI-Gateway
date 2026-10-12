using api.Repositories.KnowledgeDocuments.Dtos;

namespace api.Repositories.KnowledgeDocuments.Interfaces;

public interface IKnowledgeDocumentRepository
{
    Task<KnowledgeDocumentDto> CreateAsync(CreateKnowledgeDocumentDto dto, CancellationToken cancellationToken = default);

    Task<List<KnowledgeDocumentDto>> GetAllAsync(Guid apiKeyId, Guid? knowledgeGroupId, CancellationToken cancellationToken = default);

    Task<List<Guid>> GetIdsAsync(Guid apiKeyId, Guid? knowledgeGroupId, CancellationToken cancellationToken = default);

    Task<KnowledgeDocumentFileDto?> GetFileAsync(Guid apiKeyId, Guid id, CancellationToken cancellationToken = default);

    Task<Stream?> OpenFileAsync(Guid apiKeyId, Guid id, CancellationToken cancellationToken = default);

    Task<KnowledgeDocumentDto?> MarkFailedAsync(Guid id, string errorMessage, CancellationToken cancellationToken = default);

    Task<KnowledgeDocumentDto?> MarkIndexedAsync(Guid id, IndexKnowledgeDocumentDto dto, CancellationToken cancellationToken = default);

    Task<List<KnowledgeChunkMatchDto>> SearchAsync(Guid apiKeyId, List<double> embedding, int topK, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid apiKeyId, Guid id, CancellationToken cancellationToken = default);
}
