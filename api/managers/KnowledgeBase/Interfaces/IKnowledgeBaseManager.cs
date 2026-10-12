using api.Managers.KnowledgeBase.Dtos;
using api.Repositories.KnowledgeDocuments.Dtos;

namespace api.Managers.KnowledgeBase.Interfaces;

public interface IKnowledgeBaseManager
{
    Task<UploadDocumentResultDto> UploadDocumentAsync(UploadDocumentDto dto, CancellationToken cancellationToken = default);

    Task<bool> DeleteGroupAsync(Guid apiKeyId, Guid knowledgeGroupId, CancellationToken cancellationToken = default);

    Task<List<KnowledgeChunkMatchDto>> SearchAsync(Guid apiKeyId, string query, int? topK, CancellationToken cancellationToken = default);

    Task<RagChatResultDto> RagChatAsync(RagChatDto dto, CancellationToken cancellationToken = default);
}
