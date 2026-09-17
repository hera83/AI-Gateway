using System.Security.Cryptography;
using AiGateway.Data;
using AiGateway.Data.Entities;
using AiGateway.Service.Keys.Dtos;
using AiGateway.Service.Keys.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AiGateway.Service.Keys;

public class KeyService(AppDbContext dbContext) : IKeyService
{
    // Prefix makes leaked keys recognizable (e.g. by secret scanners), like GitHub's "ghp_" or Stripe's "sk_".
    private const string KeyPrefix = "agw_";

    public async Task<CreatedKeyDto> CreateAsync(CreateKeyDto request, CancellationToken cancellationToken)
    {
        var (plainTextKey, hash) = GenerateKey();

        var entity = new ApiKey
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            ResponsibleName = request.ResponsibleName,
            ContactInfo = request.ContactInfo,
            ExpiresAt = request.ExpiresAt,
            KeyHash = hash,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.ApiKeys.Add(entity);
        AddAuditLog(entity.Id, ApiKeyAuditAction.Created, $"Nøgle '{entity.Name}' oprettet.");
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreatedKeyDto
        {
            Id = entity.Id,
            Name = entity.Name,
            ResponsibleName = entity.ResponsibleName,
            ContactInfo = entity.ContactInfo,
            IsActive = entity.IsActive,
            ExpiresAt = entity.ExpiresAt,
            CreatedAt = entity.CreatedAt,
            LastRotatedAt = entity.LastRotatedAt,
            LastUsedAt = entity.LastUsedAt,
            ApiKey = plainTextKey
        };
    }

    public async Task<IReadOnlyList<KeyDto>> ListAsync(CancellationToken cancellationToken)
    {
        var entities = await dbContext.ApiKeys.OrderBy(key => key.Name).ToListAsync(cancellationToken);
        return entities.Select(ToDto).ToList();
    }

    public async Task<KeyDto> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        ToDto(await FindAsync(id, cancellationToken));

    public async Task<KeyDto> UpdateAsync(Guid id, UpdateKeyDto request, CancellationToken cancellationToken)
    {
        var entity = await FindAsync(id, cancellationToken);

        entity.Name = request.Name;
        entity.ResponsibleName = request.ResponsibleName;
        entity.ContactInfo = request.ContactInfo;
        entity.ExpiresAt = request.ExpiresAt;

        AddAuditLog(entity.Id, ApiKeyAuditAction.Updated, $"Nøgle '{entity.Name}' opdateret.");
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(entity);
    }

    public async Task<RolledOverKeyDto> RolloverAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await FindAsync(id, cancellationToken);
        var (plainTextKey, hash) = GenerateKey();

        entity.KeyHash = hash;
        entity.LastRotatedAt = DateTimeOffset.UtcNow;

        // Rollover doubles as "renew", same as Activate — always pushes ExpiresAt a year out, so
        // rotating an already-expired key's secret also makes it usable again instead of silently
        // handing back a plaintext secret that can never authenticate.
        entity.ExpiresAt = DateTimeOffset.UtcNow.AddYears(1);

        AddAuditLog(entity.Id, ApiKeyAuditAction.RolledOver, $"Nøgle '{entity.Name}' roteret, ny udløbsdato: {entity.ExpiresAt:yyyy-MM-dd}.");
        await dbContext.SaveChangesAsync(cancellationToken);

        return new RolledOverKeyDto
        {
            Id = entity.Id,
            ApiKey = plainTextKey,
            LastRotatedAt = entity.LastRotatedAt.Value
        };
    }

    public async Task<KeyDto> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var entity = await FindAsync(id, cancellationToken);
        entity.IsActive = isActive;

        // Activate doubles as "renew", same as Rollover — always pushes ExpiresAt a year out, so
        // reactivating an expired key makes it usable again. Deactivate mirrors this by setting
        // ExpiresAt to now, so a deactivated key also reads as expired everywhere (audit log,
        // listings) rather than just IsActive-false.
        var action = isActive ? ApiKeyAuditAction.Activated : ApiKeyAuditAction.Deactivated;
        string verb;
        if (isActive)
        {
            entity.ExpiresAt = DateTimeOffset.UtcNow.AddYears(1);
            verb = $"aktiveret, ny udløbsdato: {entity.ExpiresAt:yyyy-MM-dd}";
        }
        else
        {
            entity.ExpiresAt = DateTimeOffset.UtcNow;
            verb = "deaktiveret";
        }

        AddAuditLog(entity.Id, action, $"Nøgle '{entity.Name}' {verb}.");
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(entity);
    }

    public async Task<IReadOnlyList<AuditLogEntryDto>> GetAuditLogAsync(Guid id, CancellationToken cancellationToken)
    {
        await FindAsync(id, cancellationToken);

        // Ordered client-side: SQLite can't translate ORDER BY over a DateTimeOffset column.
        var entries = await dbContext.ApiKeyAuditLogs
            .Where(log => log.ApiKeyId == id)
            .ToListAsync(cancellationToken);

        return entries
            .OrderByDescending(log => log.Timestamp)
            .Select(log => new AuditLogEntryDto
            {
                Action = log.Action.ToString(),
                Timestamp = log.Timestamp,
                Details = log.Details
            })
            .ToList();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await FindAsync(id, cancellationToken);

        // Hard delete, by explicit request — cascades (see AppDbContext.OnModelCreating) to the
        // key's own audit log and to any knowledge-base groups/documents/chunks created under it.
        dbContext.ApiKeys.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<ApiKey> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.ApiKeys.FirstOrDefaultAsync(key => key.Id == id, cancellationToken)
            ?? throw new ApiKeyNotFoundException(id);

    private void AddAuditLog(Guid apiKeyId, ApiKeyAuditAction action, string details) =>
        dbContext.ApiKeyAuditLogs.Add(new ApiKeyAuditLog
        {
            Id = Guid.NewGuid(),
            ApiKeyId = apiKeyId,
            Action = action,
            Timestamp = DateTimeOffset.UtcNow,
            Details = details
        });

    private static (string PlainTextKey, string Hash) GenerateKey()
    {
        var plainTextKey = $"{KeyPrefix}{Convert.ToHexString(RandomNumberGenerator.GetBytes(32))}";
        return (plainTextKey, ApiKeyHasher.Hash(plainTextKey));
    }

    private static KeyDto ToDto(ApiKey entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        ResponsibleName = entity.ResponsibleName,
        ContactInfo = entity.ContactInfo,
        IsActive = entity.IsActive,
        ExpiresAt = entity.ExpiresAt,
        CreatedAt = entity.CreatedAt,
        LastRotatedAt = entity.LastRotatedAt,
        LastUsedAt = entity.LastUsedAt
    };
}
