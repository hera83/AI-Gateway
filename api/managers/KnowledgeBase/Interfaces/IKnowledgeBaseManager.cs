using api.Managers.KnowledgeBase.Dtos;
using api.Repositories.KnowledgeDocuments.Dtos;

namespace api.Managers.KnowledgeBase.Interfaces;

public interface IKnowledgeBaseManager
{
    Task<UploadDocumentResultDto> UploadDocumentAsync(UploadDocumentDto dto, CancellationToken cancellationToken = default);

    // Processes the oldest document waiting in "Processing", or else adds Markdown to the oldest indexed document
    // without it. false = nothing was waiting.
    Task<bool> ProcessNextDocumentAsync(CancellationToken cancellationToken = default);

    // Lays out the Markdown of the oldest mostly-OCR document with the LLM. false = nothing was waiting.
    Task<bool> ImproveNextMarkdownAsync(CancellationToken cancellationToken = default);

    Task<bool> DeleteGroupAsync(Guid apiKeyId, Guid knowledgeGroupId, CancellationToken cancellationToken = default);

    Task<List<KnowledgeChunkMatchDto>?> SearchAsync(Guid apiKeyId, string query, int? topK, Guid? knowledgeGroupId, CancellationToken cancellationToken = default);

    // Researches a task in the key's documents (or one group): asks questions back while the task is unclear, otherwise
    // searches in rounds and answers with citations. null = the group doesn't exist.
    Task<AgentChatResultDto?> AgentChatAsync(AgentChatDto dto, CancellationToken cancellationToken = default);

    Task<RagChatResultDto?> RagChatAsync(RagChatDto dto, CancellationToken cancellationToken = default);
}
