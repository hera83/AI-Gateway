using api.Data;
using api.Repositories.KnowledgeGroups.Dtos;
using api.Repositories.KnowledgeGroups.Interfaces;
using api.Repositories.KnowledgeGroups.Mappers;
using Microsoft.EntityFrameworkCore;

namespace api.Repositories.KnowledgeGroups;

// Every query is scoped to the calling key's own ApiKeyId, so another key's groups simply don't exist.
public class KnowledgeGroupRepository(ApiDbContext dbContext) : IKnowledgeGroupRepository
{
    public async Task<KnowledgeGroupDto?> CreateAsync(CreateKnowledgeGroupDto dto, CancellationToken cancellationToken = default)
    {
        if (await dbContext.KnowledgeGroups.AnyAsync(x => x.ApiKeyId == dto.ApiKeyId && x.Name == dto.Name, cancellationToken))
        {
            return null;
        }

        var entity = dto.ToEntity();
        dbContext.KnowledgeGroups.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public Task<List<KnowledgeGroupDto>> GetAllAsync(Guid apiKeyId, CancellationToken cancellationToken = default) =>
        dbContext.KnowledgeGroups.AsNoTracking()
            .Where(x => x.ApiKeyId == apiKeyId)
            .OrderBy(x => x.Name)
            .Select(KnowledgeGroupMapper.ToDtoExpression)
            .ToListAsync(cancellationToken);

    public Task<KnowledgeGroupDto?> GetByIdAsync(Guid apiKeyId, Guid id, CancellationToken cancellationToken = default) =>
        dbContext.KnowledgeGroups.AsNoTracking()
            .Where(x => x.ApiKeyId == apiKeyId && x.Id == id)
            .Select(KnowledgeGroupMapper.ToDtoExpression)
            .FirstOrDefaultAsync(cancellationToken);

    // The database cascades to the group's document and chunk rows. Vectors and files are not touched
    // here; IKnowledgeBaseManager.DeleteGroupAsync deletes the documents first.
    public async Task<bool> DeleteAsync(Guid apiKeyId, Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.KnowledgeGroups
            .Where(x => x.ApiKeyId == apiKeyId && x.Id == id)
            .ExecuteDeleteAsync(cancellationToken) > 0;
}
