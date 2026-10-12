using api.Managers.ApiKeys.Interfaces;
using api.Repositories.ApiKeys.Interfaces;
using api.Repositories.KnowledgeDocuments.Interfaces;

namespace api.Managers.ApiKeys;

public class ApiKeyManager(
    IApiKeyRepository apiKeyRepository,
    IKnowledgeDocumentRepository knowledgeDocumentRepository) : IApiKeyManager
{
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (await apiKeyRepository.GetByIdAsync(id, cancellationToken) is null)
        {
            return false;
        }

        // The key delete cascades to rows only, so the key's documents are deleted first to remove their
        // vectors (vectors.db) and files (app_files/) as well.
        foreach (var documentId in await knowledgeDocumentRepository.GetIdsAsync(id, null, cancellationToken))
        {
            await knowledgeDocumentRepository.DeleteAsync(id, documentId, cancellationToken);
        }

        return await apiKeyRepository.DeleteAsync(id, cancellationToken);
    }
}
