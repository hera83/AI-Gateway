using api.Repositories.KnowledgeGroups.Dtos;

namespace api.Repositories.KnowledgeGroups.Interfaces;

public interface IKnowledgeGroupRepository
{
    Task<KnowledgeGroupDto?> CreateAsync(CreateKnowledgeGroupDto dto, CancellationToken cancellationToken = default);

    Task<List<KnowledgeGroupDto>> GetAllAsync(Guid apiKeyId, CancellationToken cancellationToken = default);

    Task<KnowledgeGroupDto?> GetByIdAsync(Guid apiKeyId, Guid id, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid apiKeyId, Guid id, CancellationToken cancellationToken = default);
}
