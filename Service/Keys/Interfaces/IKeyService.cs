using AiGateway.Service.Keys.Dtos;

namespace AiGateway.Service.Keys.Interfaces;

public interface IKeyService
{
    Task<CreatedKeyDto> CreateAsync(CreateKeyDto request, CancellationToken cancellationToken);

    Task<IReadOnlyList<KeyDto>> ListAsync(CancellationToken cancellationToken);

    Task<KeyDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<KeyDto> UpdateAsync(Guid id, UpdateKeyDto request, CancellationToken cancellationToken);

    Task<RolledOverKeyDto> RolloverAsync(Guid id, CancellationToken cancellationToken);

    Task<KeyDto> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditLogEntryDto>> GetAuditLogAsync(Guid id, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
