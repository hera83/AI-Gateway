using System.Linq.Expressions;
using api.Data.Models;
using api.Repositories.KnowledgeGroups.Dtos;

namespace api.Repositories.KnowledgeGroups.Mappers;

public static class KnowledgeGroupMapper
{
    public static readonly Expression<Func<KnowledgeGroup, KnowledgeGroupDto>> ToDtoExpression = x => new KnowledgeGroupDto
    {
        Id = x.Id,
        Name = x.Name,
        CreatedAt = x.CreatedAt
    };

    private static readonly Func<KnowledgeGroup, KnowledgeGroupDto> CompiledToDto = ToDtoExpression.Compile();

    public static KnowledgeGroupDto ToDto(this KnowledgeGroup entity) => CompiledToDto(entity);

    public static KnowledgeGroup ToEntity(this CreateKnowledgeGroupDto dto) => new()
    {
        ApiKeyId = dto.ApiKeyId,
        Name = dto.Name
    };
}
