using api.Repositories.KnowledgeDocuments.Dtos;

namespace api.Repositories.KnowledgeDocuments.Interfaces;

public interface IKnowledgeDocumentRepository
{
    Task<KnowledgeDocumentDto> CreateAsync(CreateKnowledgeDocumentDto dto, CancellationToken cancellationToken = default);

    Task<List<KnowledgeDocumentDto>> GetAllAsync(Guid apiKeyId, Guid? knowledgeGroupId, CancellationToken cancellationToken = default);

    Task<List<Guid>> GetIdsAsync(Guid apiKeyId, Guid? knowledgeGroupId, CancellationToken cancellationToken = default);

    Task<KnowledgeDocumentDto?> GetNextProcessingAsync(CancellationToken cancellationToken = default);

    Task<KnowledgeDocumentDto?> GetNextWithOutdatedMarkdownAsync(int currentVersion, CancellationToken cancellationToken = default);

    Task<KnowledgeDocumentMarkdownDto?> GetMarkdownAsync(Guid apiKeyId, Guid id, CancellationToken cancellationToken = default);

    Task<KnowledgeDocumentLlmMarkdownDto?> GetNextMarkdownLlmPendingAsync(int currentVersion, CancellationToken cancellationToken = default);

    Task<bool> SetMarkdownAsync(Guid id, string markdown, bool needsLlm, int version, CancellationToken cancellationToken = default);

    Task<bool> SaveLlmMarkdownProgressAsync(Guid id, string draft, int partsDone, int partsTotal, CancellationToken cancellationToken = default);

    Task<bool> SetLlmMarkdownAsync(Guid id, string markdown, CancellationToken cancellationToken = default);

    Task<bool> SkipLlmMarkdownAsync(Guid id, CancellationToken cancellationToken = default);

    Task<KnowledgeDocumentTextDto?> GetTextAsync(Guid apiKeyId, Guid id, CancellationToken cancellationToken = default);

    Task<KnowledgeDocumentFileDto?> GetFileAsync(Guid apiKeyId, Guid id, CancellationToken cancellationToken = default);

    Task<Stream?> OpenFileAsync(Guid apiKeyId, Guid id, CancellationToken cancellationToken = default);

    Task<int> DeleteOrphanedFilesAsync(TimeSpan minAge, CancellationToken cancellationToken = default);

    Task<KnowledgeDocumentDto?> MarkFailedAsync(Guid id, string errorMessage, CancellationToken cancellationToken = default);

    Task<bool> ReplaceChunksAsync(Guid id, List<CreateKnowledgeChunkDto> chunks, int indexVersion, CancellationToken cancellationToken = default);

    Task<KnowledgeDocumentDto?> GetNextWithOutdatedIndexAsync(int currentVersion, CancellationToken cancellationToken = default);

    Task<KnowledgeDocumentDto?> MarkIndexedAsync(Guid id, IndexKnowledgeDocumentDto dto, CancellationToken cancellationToken = default);

    Task<List<KnowledgeChunkMatchDto>> SearchAsync(Guid apiKeyId, List<double> embedding, int topK, Guid? knowledgeGroupId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid apiKeyId, Guid id, CancellationToken cancellationToken = default);
}
