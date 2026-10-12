using System.Linq.Expressions;
using api.Data.Models;
using api.Repositories.ApiKeys.Dtos;

namespace api.Repositories.ApiKeys.Mappers;

public static class ApiKeyMapper
{
    public static readonly Expression<Func<ApiKey, ApiKeyDto>> ToDtoExpression = x => new ApiKeyDto
    {
        Id = x.Id,
        Name = x.Name,
        ResponsibleName = x.ResponsibleName,
        ContactInfo = x.ContactInfo,
        KeyPreview = x.KeyPreview,
        IsActive = x.IsActive,
        ExpiresAt = x.ExpiresAt,
        CreatedAt = x.CreatedAt,
        UpdatedAt = x.UpdatedAt,
        RolledOverAt = x.RolledOverAt,
        LastUsedAt = x.LastUsedAt
    };

    private static readonly Func<ApiKey, ApiKeyDto> CompiledToDto = ToDtoExpression.Compile();

    public static ApiKeyDto ToDto(this ApiKey entity) => CompiledToDto(entity);

    public static ApiKey ToEntity(this CreateApiKeyDto dto) => new()
    {
        Name = dto.Name,
        ResponsibleName = dto.ResponsibleName,
        ContactInfo = dto.ContactInfo,
        ExpiresAt = dto.ExpiresAt,
        KeyHash = dto.KeyHash,
        KeyPreview = dto.KeyPreview,
        IsActive = true
    };

    public static void ApplyTo(this UpdateApiKeyDto dto, ApiKey entity)
    {
        entity.Name = dto.Name;
        entity.ResponsibleName = dto.ResponsibleName;
        entity.ContactInfo = dto.ContactInfo;
        entity.ExpiresAt = dto.ExpiresAt;
    }
}
