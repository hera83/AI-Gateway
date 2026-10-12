using api.Repositories.ApiKeys.Dtos;

namespace api.Repositories.ApiKeys.Interfaces;

public interface IApiKeyRepository
{
    Task<ApiKeyDto> CreateAsync(CreateApiKeyDto dto, CancellationToken cancellationToken = default);

    Task<List<ApiKeyDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ApiKeyDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ApiKeyDto?> GetValidByHashAsync(string keyHash, CancellationToken cancellationToken = default);

    Task<ApiKeyDto?> UpdateAsync(Guid id, UpdateApiKeyDto dto, CancellationToken cancellationToken = default);

    Task<ApiKeyDto?> RolloverAsync(Guid id, string keyHash, string keyPreview, CancellationToken cancellationToken = default);

    Task<ApiKeyDto?> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);

    Task<List<ApiKeyAuditLogDto>?> GetAuditLogAsync(Guid id, CancellationToken cancellationToken = default);

    Task MarkUsedAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
